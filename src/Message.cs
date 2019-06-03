using System;

namespace SkypeHistoryStats
{
    internal enum MessageTypes
    {
        Call,
        CallEnd,
        File,
        Removed,
        Status,
        System,
        Text
    }

    /// <summary>Represents a Skype Message containing info, such as the sender, its date and its content.</summary>
    internal class Message : IEquatable<Message>
    {
        /// <summary>Initializes a new instance of the <see cref="Message" /> class.</summary>
        /// <param name="value">The value content of the message.</param>
        /// <param name="sender">The name of the sender of the message.</param>
        /// <param name="sendDate">The date and time the message was sent.</param>
        /// <param name="type">The type of the current message.</param>
        public Message(string value, string sender, DateTime sendDate, MessageTypes type)
        {
            Value = value;
            Sender = sender;
            SendDate = sendDate;
            Type = type;
        }

        /// <summary>Gets the date and time when the message was sent.</summary>
        public DateTime SendDate { get; }

        /// <summary>Gets the name of the sender of the message.</summary>
        public string Sender { get; }

        /// <summary>Gets the message content.</summary>
        public string Value { get; }

        public MessageTypes Type { get; }

        #region Implemented

        public bool Equals(Message otherMessage)
        {
            if (ReferenceEquals(this, otherMessage))
            {
                return true;
            }

            if (otherMessage == null)
            {
                return false;
            }

            return SendDate == otherMessage.SendDate && Sender == otherMessage.Sender &&
                   Value == otherMessage.Value;
        }

        public override bool Equals(object otherObject)
        {
            if (otherObject == null)
            {
                return false;
            }

            return otherObject.GetType() == GetType() && Equals((Message) otherObject);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SendDate.GetHashCode();
                hashCode = (hashCode * 397) ^ Sender.GetHashCode();
                hashCode = (hashCode * 397) ^ Value.GetHashCode();

                return hashCode;
            }
        }

        #endregion
    }
}
