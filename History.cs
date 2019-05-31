using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SkypeHistoryStats
{
    /// <summary>Represents a Skype history and holds its messages.</summary>
    internal class History
    {
        /// <summary>Initializes a new instance of the <see cref="History" /> class from messages.</summary>
        /// <param name="messages">The messages in the history.</param>
        public History(List<Message> messages)
        {
            Messages = messages;
        }

        /// <summary>Gets the messages of the current history.</summary>
        public List<Message> Messages { get; }

        #region Public Methods
        /// <summary>Parses the specified source files to a History object.</summary>
        /// <param name="sourceFiles">The text files, which contain the Skype history.</param>
        /// <returns>A <see cref="History" /> object constructed from the parsed messages.</returns>
        public static History Parse(IEnumerable<string> sourceFiles)
        {
            // Parse messages from files and aggregate the Lists in order
            var historyMessages = sourceFiles.Select(fileName => ParseMessages(File.ReadAllText(fileName)))
                .OrderBy(messages => messages[0].SendDate)
                .Aggregate(new List<Message>(), (histories, messages) =>
                {
                    histories.AddRange(messages);
                    return histories;
                });

            return new History(historyMessages);
        }

        /// <summary>Creates a User array from the specified messages.</summary>
        /// <param name="messages">The messages to be used to construct the <see cref="User" /> array.</param>
        /// <returns>The constructed <see cref="User" /> array.</returns>
        public User[] GetUsers(IEnumerable<Message> messages)
        {
            // User aliases for the Minecraaaft Skype group
            var userAliases = new[]
            {
                new[] {"Donkó István (Isti115)", "siti", "Donkó Istv\ufffd\ufffdn (Isti115)"},
                new[] {"Geiszl András", "Geiszl Andr\ufffd\ufffds"},
                new[] {"Inges Balázs", "balazs9 _"},
                new[] {"Inges Tamás", "Inges Tomi", "Inges Tam\ufffd\ufffds"},
                new[] {"Norticus", "Nyeste Todi"},
                new[] {"Sali Ádám", "Ádám ︻デ═一"},
                new[] {"Vitanov George", "vitanov.george"},
                new[] {"Walrusz", "Norbi", "=NΘRBΨ="}
            };

            // Group messages by users, merge groups belonging to the same user and sort result by message count
            var users = messages.GroupBy(message => message.Sender)
                .Aggregate(new List<User>(), (uniqueSenderList, senderGroup) =>
                {
                    // Get already added user, which belongs to the current group
                    var sameUser = uniqueSenderList.FirstOrDefault(user =>
                    {
                        // Check if sender name is just a different alias
                        var isAlias = userAliases.FirstOrDefault(aliases => aliases.Contains(senderGroup.Key))
                                          ?.Contains(user.Names[0]) ?? false;

                        // Check if the parts of the names are the same (e.g. John Francis Doe == Doe John Francis)
                        var isNamePartsSame = new HashSet<string>(user.Names[0].ToLower().Split(" "))
                            .SetEquals(senderGroup.Key.ToLower().Split(" "));

                        return isAlias || isNamePartsSame;
                    });

                    if (sameUser != null)
                    {
                        // If such user is found, append the name and messages of the current group to it
                        sameUser.Names.Add(senderGroup.Key);
                        sameUser.Messages.AddRange(senderGroup);
                    }
                    else
                    {
                        // If it wasn't found, create a new User from the current group and add it to the list
                        var newSender = new User(new List<string> {senderGroup.Key}, senderGroup.ToList());
                        uniqueSenderList.Add(newSender);
                    }

                    return uniqueSenderList;
                })
                .OrderByDescending(user => user.Messages.Count)
                .ToArray();

            return users;
        }
        #endregion

        #region Private Methods
        private static List<Message> ParseMessages(string history)
        {
            // Regex for matching date and message headers
            var dateRegex = new Regex(@"^[a-zA-Z]+, (\d{2}\. [a-zA-Z]+ \d{4})\r?$\n-{40}", RegexOptions.Multiline);
            var messageRegex = new Regex(@"^(\d{2}:\d{2})\s+([^:]*):\r?$", RegexOptions.Multiline);

            // Go through all the dates found in history
            var dateMatches = dateRegex.Matches(history);
            var messages = new List<Message>();
            for (var i = 0; i < dateMatches.Count; i++)
            {
                // Parse the date
                var currentDate = DateTime.Parse(dateMatches[i].Groups[1].Value);

                // Get the content under the current date (i.e. the messages)
                var (dayContentStart, dayContentLength) = GetStartAndLength(dateMatches, i, history.Length);
                var dayContent = history.Substring(dayContentStart, dayContentLength);

                // Go through all the messages in the current date
                var messageMatches = messageRegex.Matches(dayContent);
                for (var j = 0; j < messageMatches.Count; j++)
                {
                    // Get the content under the current message (i.e. the message texts)
                    var (messageContentStart, messageContentLength) =
                        GetStartAndLength(messageMatches, j, dayContent.Length);
                    var messageContent = dayContent.Substring(messageContentStart, messageContentLength).Trim()
                        .Replace("\r", "").Replace("\n", " ");

                    // Parse sender and send date and create a new Message object
                    var sender = messageMatches[j].Groups[2].Value;
                    var sendDate = currentDate.Add(TimeSpan.Parse(messageMatches[j].Groups[1].Value));

                    messages.Add(new Message(messageContent, sender, sendDate));
                }
            }

            return messages;
        }

        private static (int, int) GetStartAndLength(IReadOnlyList<Match> matches, int index, int stringLength)
        {
            // Get substring start and length of the content between two (specified) matches
            var dayContentStart = matches[index].Index + matches[index].Length;
            var dayContentEnd = index < matches.Count - 1 ? matches[index + 1].Index : stringLength;

            return (dayContentStart, dayContentEnd - dayContentStart);
        }
        #endregion
    }
}
