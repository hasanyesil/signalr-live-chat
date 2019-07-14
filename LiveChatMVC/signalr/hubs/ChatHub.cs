using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiveChatMVC.Models;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json.Linq;
using LiveChatMVC.DummyApi;
using User = LiveChatMVC.Models.User;
using System.Collections.Concurrent;
using System.Data.Entity;

namespace LiveChatMVC.signalr.hubs
{
    // PS: When client call function from hub, server creates ChatHub object instance.
    // TODO: Need exception handling. 
    public class ChatHub : Hub
    {
        // This dictionary contains data about which user connected to hub from which client id.
        // key = connectionId, value = Users object. We use ConcurrentDictionary because of thread safety.
        // We have to update this dictionary when user connected or when user disconnected or when user 
        // update her/his connection id like after refleshing the page
        private static ConcurrentDictionary<string, Users> SignalRUsers = new ConcurrentDictionary<string, Users>();

        private string jsonStringUser; // Json string from api contains user information.
        private JObject jsonUser;   // Json object for user informations.

        static readonly Gldnhrn api = new Gldnhrn(); // Dummy api looks like goldenhorn api
        static readonly ChatContext db = new ChatContext(); // Database

        // PS: You have to override onConnect function normally but its example.
        
        public void Connect(string senderToken)
        {
            // Fetching jsonstring contains user information from api via token
            jsonStringUser = api.requestUser(senderToken);

            // Is user exist or not 
            if (jsonStringUser == null)
                return;

            // Convert JSON string to object
            jsonUser = JObject.Parse(jsonStringUser);

            // Initialiazing variables
            int userId = int.Parse(jsonUser["userId"].ToString());
            string userName = jsonUser["userName"].ToString();
            string chatId = api.ticketId();
            string userRole = jsonUser["role"].ToString();

            // We cant store object in hub because when client call function from hub, server create hub instance and process the function. After that
            // server remove hub intance. So out assigned variables will remove with hub instance. You cannot access any data again.(except static variables.)
            // So store it in CallerState if u want to access variables from outside the functions
            // PS: CallerState is reachable on both client and server side.
            Clients.CallerState.userName = userName;
            Clients.CallerState.userId = userId;
            Clients.CallerState.chatId = chatId; 


            // Search user on database.
            var users = db.Users.Where(i => i.Id == userId).ToList();

            // If user doesnt exist create new user and insert it to database.
            if (users.Count < 1)
            {
                insertNewUser(userId, userName);
            }

            // Search for chatroom on database
            // PS: tickedId(assing to chatId on above) will come from GoldenHorn api so we use static chatId for this example like its come from goldenhorn api.
            // If you use ticketId as a signalr group name like us its gonna be easier.
            var chats = db.Chats.Where(i => i.Id == chatId).ToList();

            // If chat doesnt exist create new chat and add to database.
            if (chats.Count < 1)
            {
                insertNewChat(chatId);
            }

            // Insert chat and user relation to database.
            //insertChatUserRelation(chatId, userId);

            // If user exist in chat but his/her connection id changed.( if user reload the page while in chat etc.)
            if (SignalRUsers.Values.Any(user => (user.GroupNames.Contains(chatId) && user.UserName == userName)))
            {
                // Update users connection id.
                SignalRUsers.TryRemove(SignalRUsers.FirstOrDefault(i => i.Value.UserName == userName).Key, out Users updatedUser);
                updatedUser.ConnectionId = Context.ConnectionId;
                SignalRUsers.TryAdd(Context.ConnectionId, updatedUser);

                // Add to chat and notify chat 
                _ = joinGroup(chatId);
                Clients.Group(chatId).updateUserCount(SignalRUsers.Where(i => i.Value.GroupNames.Contains(chatId)).Count());
            }
            // If user not in chat room
            else
            {
                SignalRUsers.TryAdd(Context.ConnectionId, new Users { ConnectionId = Context.ConnectionId, UserName = userName, GroupNames = new List<string>() { chatId } });
                _ = joinGroup(chatId);
                Clients.Group(chatId).updateUserCount(SignalRUsers.Count());
            }

            // Fetch old messages where sended in chat.
            _ = fetchChatHistory(chatId, userId);
        }

        // This function works when client wants to send message to group
        public void Send(string message)
        {
            // Initialiaze variables
            string userName = Clients.CallerState.userName;
            string chatId = Clients.CallerState.chatId;

            // Update Message table on database.(Insert message to message table)

            var msg = insertNewMessage(message, chatId);

            // Send message to group.
            Clients.OthersInGroup(chatId).addNewMessageToPage(userName, message, msg.CreatedAt);

            // Show sended message on caller clients iu.
            Clients.Caller.addNewMessageToMe(userName, message, msg.CreatedAt);
        }

        private void insertNewUser(int userId, string userName)
        {
            var user = new User
            {
                Id = userId,
                Name = userName,
            };
            db.Users.Add(user);
            db.SaveChanges();
        }

        private void insertNewChat(string chatId)
        {
            var chat = new Chat
            {
                Id = chatId,
            };
            db.Chats.Add(chat);
            db.SaveChanges();
        }

        private void insertChatUserRelation(string chatId, int userId)
        {
            var userChat = new UserChat
            {
                ChatId = chatId,
                UserId = userId
            };
            db.UserChats.Add(userChat);
            db.SaveChanges();
        }

        private Message insertNewMessage(string message, string chatId)
        {
            var msg = new Message
            {
                MessageBody = message,
                ChatId = chatId,
                UserId = Convert.ToInt32(Clients.CallerState.userId),
                CreatedAt = DateTime.Now
            };

            db.Messages.Add(msg);
            db.SaveChanges();

            return msg;
        }

        //Add to group.
        private async Task joinGroup(string chatId)
        {
            await Groups.Add(Context.ConnectionId, chatId);

            // Update user count on group.
            await Clients.Group(chatId).updateUserCount(SignalRUsers.Values.Where(user => user.GroupNames.Contains(chatId)).Count());
        }

        private async Task fetchChatHistory(string chatId, int userId)
        {
            var messagesAndUsers = await (from u in db.Users
                                          join m in db.Messages on u.Id equals m.UserId
                                          select new { u, m }).Where(i => i.m.ChatId == chatId).ToListAsync();

            // If chat has not any messages, return.
            if (messagesAndUsers.Count < 1)
                return;

            // Send message histroy as an array to client.
            await Clients.Caller.printMessageHistory(messagesAndUsers, userId); ;
        }

        // This function call after 6 second timeout (for more information https://docs.microsoft.com/en-us/aspnet/signalr/overview/guide-to-the-api/handling-connection-lifetime-events).
        // So when user reload the page it takes 1 second and users client id will changed.
        // We have to handle this on connect function if we still have connection on list we have to re assign new Connection id
        // We dont need to remove client from group. Signalr makes it for us. And it's recommended to dont remove client from group manually
        public override Task OnDisconnected(bool stopCalled)
        {
            string groupName = api.ticketId();

            // If user reload the page and connect function works again, user must removed from list and users new connection id updated. So if user reload the page
            // and login in 6 seconds then connect function will remove expired user before the OnDisconnected function. We can't removed user again.
            // Check for user removed on OnDisconnect function.
            bool isRemoved = SignalRUsers.TryRemove(Context.ConnectionId, out Users disconnectedUser);

            // if isRemoved = false then it means user already updated before OnDisconnect function call. 
            // (when we reflesh the page client call connect function again and we update users conenction id. That means dictionary has no longer )
            if (isRemoved)
            {
                Clients.OthersInGroup(groupName).userLeft(disconnectedUser.UserName);
                Clients.OthersInGroup(groupName).updateUserCount(SignalRUsers.Where(i => i.Value.GroupNames.Contains(groupName)).Count());
            }
            return base.OnDisconnected(stopCalled);
        }

    }
}