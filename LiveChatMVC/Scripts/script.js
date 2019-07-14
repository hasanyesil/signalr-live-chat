// Prepairing UI
(function ($) {
    $(document).ready(function () {
        var $chatbox = $('.chatbox'),
            $chatboxTitle = $('.chatbox__title'),
            $chatboxTitleClose = $('.chatbox__title__close')
            

            $chatboxTitle.on('click', function () {
            $chatbox.toggleClass('chatbox--tray');
            unReadMessageCount = 0
            $('#message-alert').css("display", "none")
        });
        $chatboxTitleClose.on('click', function (e) {
            e.stopPropagation();
            $chatbox.toggleClass('chatbox--tray');
        })
    });

    $('#new-message-alert').click(function () {
        $('#chat_body').animate({ scrollTop: $('#chat_body')[0].scrollHeight }, 400)
    })

})(jQuery);

// Variable for handling chatHub class.
var chat = $.connection.chatHub;

var unReadMessageCount = 0
var unReadMessageCountNow = 0
// Adding message to ui. Must invoke from hub when someone send message
chat.client.addNewMessageToPage = function (name, message, creationDate) {

    unReadMessageCount++

    newMessageLeft(message, creationDate, name)

    if ($('#new-message-alert').is(':visible') == false) {
        $('#chat_body').scrollTop($('#chat_body')[0].scrollHeight)
    }

    console.log($('#chat_body').scrollTop() - $('#chat_body')[0].scrollHeight)

    if ($('#chat-window').hasClass('chatbox--tray') && unReadMessageCount > 0) {
        $('#message-alert').css("display", "block")
        $('#message-count').html(unReadMessageCount)
    }
    else {
        unReadMessageCount = 0
    }

    if (($('#chat_body').scrollTop() - $('#chat_body')[0].scrollHeight) < 287) {
        unReadMessageCountNow++
        $('#unread-message-count').css("display", "block").html(unReadMessageCountNow)
    }
}

chat.client.printMessageHistory = function (message, userId) {
    message.forEach(function (element) {

        if (!(userId == element.u.Id)) {
            newMessageLeft(element.m.MessageBody, element.m.CreatedAt, element.u.Name)
        } else {
            newMessageRight(element.m.MessageBody, element.m.CreatedAt, element.u.Name, false)
        }
        $('#chat_body').scrollTop($('#chat_body')[0].scrollHeight);
    })
}

chat.client.userLeft = function (userName) {
    userLeftAlert(userName)
}

// Add new message to callerclient
chat.client.addNewMessageToMe = function (name, message, creationDate) {
    newMessageRight(message, creationDate, name, true)
}

//Safe New Message Functions
function newMessageLeft(MessageBody, CreatedAt, Name) {
   
    
    var stringHTML = ('<div class="chatbox__body__message chatbox__body__message--left">'
        + '<div class="clearfix"></div>'
        + '<div class="ul_section_full">'
        + '<ul class="ul_msg">'
        + '<li style="font-size:10px"  class="pb-2 text-capitalize d-flex justify-content-end"><i>' + Name + '</i></li>'
        + '<li class="message">' + MessageBody + '</li>'
        + '<li class="timing"><span class="mr-2">' + formatDate(CreatedAt) + '</span><i class="fa fa-clock"></li>'
        + '</ul>'
        + '<div class="clearfix"></div>'
        + '</div>'
        + '</div>')

    var html = $.parseHTML(stringHTML)
    var chatBody = $('#chat_body')

    chatBody.append(html)
}

function newMessageRight(MessageBody, CreatedAt, Name, isMe) {

    var stringHTML = ('<div class="chatbox__body__message chatbox__body__message--right">'
        + '<div class="clearfix"></div>'
        + '<div class="ul_section_full">'
        + '<ul class="ul_msg">'
        + '<li style="font-size:10px"  class="pb-2 text-capitalize d-flex justify-content-end"><i>' + Name + '</i></li>'
        + '<li class="message">' + MessageBody + '</li>'
        + '<li class="timing"><span class="mr-2">' + formatDate(CreatedAt) + '</span><i class="fa fa-clock"></li>'
        + '</ul>'
        + '<div class="clearfix"></div>'
        + '</div>'
        + '</div>')

    var html = $.parseHTML(stringHTML)
    var chatBody = $('#chat_body')

    if (isMe) {
        chatBody.append(html).animate({ scrollTop: $('#chat_body')[0].scrollHeight }, 400)
    }
    else {
        chatBody.append(html)
    }
}

function userLeftAlert(userName) {
    var stringHTML = ('<div class="chatbox__body__message chatbox__body__message--left">'
        + '<div class="clearfix"></div>'
        + '<div class="ul_section_full_red">'
        + '<ul class="ul_msg d-flex justify-content-between">'
        + '<li class="message" style="font-size:10px">' + userName + " ayrıldı." + '</li>'
        + '<li class="timing"><span class="mr-2">' + new Date().toLocaleTimeString() + '</span><i class="fa fa-clock"></li>'
        + '</ul>'
        + '<div class="clearfix"></div>'
        + '</div>'
        + '</div>')

    var html = $.parseHTML(stringHTML)
    $('#chat_body').append(html)
}


function formatDate(date) {

    var newDate = new Date(date)
    var messageDate = moment(date, "YYYY-MM-DD");
    var current = moment().startOf('day');
    var diff = moment.duration(messageDate.diff(current)).asDays();

    var formattedDate = diff < 0 ? moment(newDate).fromNow() : moment(newDate).format("HH:mm")

    return formattedDate
}

// TODO: Show count of online users in chat.
chat.client.updateUserCount = function (count) {
    if (count < 2) {
        $('#title').addClass('gray_title')
        var html = $.parseHTML('<a href="#">Chat ( <i class="fas fa-user-tie pr-2"></i>' + (count - 1) + ' Online)</a>')
        $('#title').html(html)
    }
    else {
        $('#title').removeClass('gray_title')
        var html = $.parseHTML('<a href="#">Chat ( <i class="fas fa-user-tie pr-2"></i>' + (count - 1) + ' Online)</a>')
        $('#title').html(html)
    }
}
// When hub started succesfully or fail.
$.connection.hub.start().done(function () {

    var token = prompt('Sender Token : ', '');
    chat.server.connect(token);

    $('#btn-chat').on('click', function () {

        var value = $('#btn-input').val()
        if (value == '' || value == null) {
            console.log('Boş mesaj atılamaz!')
        }
        else {
            chat.server.send(value);
            $('#btn-input').val('');
            $('#btn-input').focus();
        }
    });
    var $chatBody = $('#chat_body')
    $chatBody.scroll(function () {
        if (($chatBody[0].scrollHeight) - $chatBody.scrollTop() > 287) {
            $('#new-message-alert').fadeIn('slow')
        }
        else {
            $('#new-message-alert').fadeOut('fast')
            $('#unread-message-count').css("display", "none")
            unReadMessageCountNow = 0
        }
    }) 
}).fail(function () {
    console.log("Hub starting fail")
});

//Enter Trigger
$('#btn-input').keyup(function (e) {
    if (e.keyCode == 13) {
        $('#btn-chat').click()
    }
})