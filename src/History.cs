using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SkypeHistoryStats
{
    internal struct DateSpan
    {
        public DateSpan(DateTime from, DateTime to)
        {
            From = from;
            To = to;
        }

        public DateTime From { get; }

        public DateTime To { get; }
    }

    /// <summary>Represents a Skype history and holds its messages.</summary>
    internal class History
    {
        private static readonly Regex DateRegex =
            new Regex(@"^[a-zA-Z]+, (\d{2}\. [a-zA-Z]+ \d{4})\r?$\n-{40}", RegexOptions.Multiline);

        private static readonly Regex MessageRegex =
            new Regex(@"^(\d{2}:\d{2})\s?([^:]*):?\r?$", RegexOptions.Multiline);

        private static readonly Regex FileSentRegex = new Regex(@"^Sent file (.*)\.$");

        private static readonly Regex LeftConversationRegex = new Regex(@"^.* has left the conversation\.$");
        private static readonly Regex ChangeConversationRegex = new Regex(@"^Changed the conversation .*$");

        private static readonly Regex StatusSendRegex = new Regex(@"^/me\s+(.*)$");

        // User aliases for the Minecraaaft Skype group
        private readonly string[][] _userAliases =
        {
            new[] {"Donát T", "Csont 01"},
            new[] {"Donkó István (Isti115)", "siti", "Donkó Istv\ufffd\ufffdn (Isti115)"},
            new[] {"Gazdag Sándor", "gsanya12"},
            new[] {"Geiszl András", "Geiszl Andr\ufffd\ufffds"},
            new[] {"Inges Balázs", "balazs9 _", "inges.balazs"},
            new[] {"Inges Tamás", "Inges Tomi", "Inges Tam\ufffd\ufffds", "﻿ｉｍｍｏｒｔａｌ", "﻿ｉｍ��ｏｒｔａｌ"},
            new[] {"Norticus", "Nyeste Todi", "ny.todi"},
            new[] {"Sali Ádám", "Ádám ︻デ═一"},
            new[] {"Vitanov George", "vitanov.george"},
            new[] {"Walrusz", "Norbi", "=NΘRBΨ=", "Norbert Szakács"}
        };

        public DateSpan[] MissingDates;

        /// <summary>Initializes a new instance of the <see cref="History" /> class from messages.</summary>
        /// <param name="messages">The messages in the history.</param>
        /// <param name="missingDates">The date ranges, where no activity was logged.</param>
        public History(List<Message> messages, DateSpan[] missingDates)
        {
            Messages = messages;
            MissingDates = missingDates;
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
                    var lastMessage = histories.LastOrDefault();
                    histories.AddRange(messages.Skip(lastMessage != null
                        ? messages.FindIndex(message => message.Equals(lastMessage)) + 1 : 0));

                    return histories;
                });

            var missingDates = new List<DateSpan>();
            var lastDate = historyMessages[0].SendDate.Date;
            foreach (var message in historyMessages)
            {
                var messageDate = message.SendDate.Date;
                if (messageDate <= lastDate.Date)
                {
                    continue;
                }

                lastDate = lastDate.AddDays(1);
                if (messageDate <= lastDate.Date)
                {
                    continue;
                }

                missingDates.Add(new DateSpan(lastDate, messageDate));
                lastDate = message.SendDate.Date;
            }

            return new History(historyMessages, missingDates.ToArray());
        }

        public (User, int)[] GetUserWordCounts(IEnumerable<Message> messages)
        {
            // Group messages by users, merge groups belonging to the same user and sort result by message count
            var users = messages.GroupBy(message => message.Sender)
                .Aggregate(new List<User>(), SameUserAggregation)
                .Select(user => (user,
                    user.Messages.Aggregate(0, (wordCount, message) => wordCount + message.Value.Split(' ').Length)))
                .OrderByDescending(userTuple => userTuple.Item2)
                .ToArray();

            return users;
        }

        /// <summary>Creates a User array from the specified messages.</summary>
        /// <param name="messages">The messages to be used to construct the <see cref="User" /> array.</param>
        /// <returns>The constructed <see cref="User" /> array.</returns>
        public (User, int)[] GetUserMessageCounts(IEnumerable<Message> messages)
        {
            // Group messages by users, merge groups belonging to the same user and sort result by message count
            var users = messages.GroupBy(message => message.Sender)
                .OrderByDescending(senderGroup => senderGroup.Count())
                .Aggregate(new List<User>(), SameUserAggregation)
                .Select(user => (user, user.Messages.Count))
                .OrderByDescending(userTuple => userTuple.Count)
                .ToArray();

            return users;
        }

        private List<User> SameUserAggregation(List<User> uniqueSenderList, IGrouping<string, Message> senderGroup)
        {
            // Get already added user, which belongs to the current group
            var sameUser = uniqueSenderList.FirstOrDefault(user =>
            {
                // Check if sender name is just a different alias
                var isAlias = _userAliases.FirstOrDefault(aliases => aliases.Contains(senderGroup.Key))
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
        }

        #endregion

        #region Private Methods

        private static List<Message> ParseMessages(string history)
        {
            var dateMatches = DateRegex.Matches(history);
            var messages = new List<Message>();

            // Go through all the dates found in history
            for (var i = 0; i < dateMatches.Count; i++)
            {
                // Parse the date
                var currentDate = DateTime.Parse(dateMatches[i].Groups[1].Value);

                // Get the content under the current date (i.e. the messages)
                var (dayContentStart, dayContentLength) = GetStartAndLength(dateMatches, i, history.Length);
                var dayContent = history.Substring(dayContentStart, dayContentLength);

                // Go through all the messages in the current date
                var messageMatches = MessageRegex.Matches(dayContent);
                for (var j = 0; j < messageMatches.Count; j++)
                {
                    // Get the content under the current message (i.e. the message texts)
                    var (messageContentStart, messageContentLength) =
                        GetStartAndLength(messageMatches, j, dayContent.Length);

                    // Parse sender and send date and create a new Message object
                    var sender = messageMatches[j].Groups[2].Value;
                    var sendDate = currentDate.Add(TimeSpan.Parse(messageMatches[j].Groups[1].Value));

                    var messageContent = dayContent.Substring(messageContentStart, messageContentLength)
                        .Trim('\n', '\r').Replace("\r", "").Replace("\n", " ");

                    var (messageType, updatedContent) = GetTypeAndUpdateContent(messageContent, sender);

                    messages.Add(new Message(updatedContent, sender, sendDate, messageType));
                }
            }

            return messages;
        }

        private static (MessageTypes, string) GetTypeAndUpdateContent(string messageContent, string sender)
        {
            var messageType = MessageTypes.Text;

            switch (messageContent)
            {
                case "[Call ended]":
                    messageType = MessageTypes.CallEnd;
                    break;
                case "[Call]":
                    messageType = MessageTypes.Call;
                    break;
                default:
                {
                    if (sender == "")
                    {
                        messageType = MessageTypes.Status;
                    }
                    else if (messageContent == "This message has been removed.")
                    {
                        messageContent = "";
                        messageType = MessageTypes.Removed;
                    }
                    else
                    {
                        var fileSentMatch = FileSentRegex.Match(messageContent);

                        if (fileSentMatch.Success)
                        {
                            messageType = MessageTypes.File;
                            messageContent = fileSentMatch.Groups[1].Value;
                        }
                        else if (LeftConversationRegex.IsMatch(messageContent)
                                 || ChangeConversationRegex.IsMatch(messageContent))
                        {
                            messageType = MessageTypes.System;
                        }
                        else
                        {
                            var statusMatch = StatusSendRegex.Match(messageContent);

                            if (statusMatch.Success)
                            {
                                messageType = MessageTypes.Status;
                                messageContent = statusMatch.Groups[1].Value;
                            }
                        }
                    }

                    break;
                }
            }

            return (messageType, messageContent);
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
