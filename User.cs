using System.Collections.Generic;

namespace SkypeHistoryStats
{
    /// <summary>Represents a Skype user and holds information, such as its usernames and messages.</summary>
    internal class User
    {
        /// <summary>Initializes a new instance of the <see cref="User" /> class.</summary>
        /// <param name="names">The names the user appears in the message history.</param>
        /// <param name="messages">The messages from the history this user has sent.</param>
        public User(List<string> names, List<Message> messages)
        {
            Names = names;
            Messages = messages;
        }

        /// <summary>Gets the names of the user.</summary>
        public List<string> Names { get; }

        /// <summary>Gets the messages the user has sent.</summary>
        public List<Message> Messages { get; }
    }
}
