using System;

namespace SkypeHistoryStats
{
    /// <summary>Represents a Skype Message containing info, such as the sender, its date and its content.</summary>
    internal class Message
    {
        /// <summary>Initializes a new instance of the <see cref="Message" /> class.</summary>
        /// <param name="text">The text content of the message.</param>
        /// <param name="sender">The name of the sender of the message.</param>
        /// <param name="sendDate">The date and time the message was sent.</param>
        public Message(string text, string sender, DateTime sendDate)
        {
            Text = text;
            Sender = sender;
            SendDate = sendDate;
        }

        /// <summary>Gets the date and time when the message was sent.</summary>
        public DateTime SendDate { get; }

        /// <summary>Gets the name of the sender of the message.</summary>
        public string Sender { get; }

        /// <summary>Gets the message content.</summary>
        public string Text { get; }
    }
}
