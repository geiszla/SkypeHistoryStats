using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ConsoleTables;

namespace SkypeHistoryStats
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            const string defaultDataFolder = @".\data";

            // Check if history files are given correctly and get their path
            string[] inputFileNames;
            if (args.Length > 1)
            {
                inputFileNames = args.Skip(1).ToArray();

                foreach (var fileName in inputFileNames)
                {
                    if (File.Exists(fileName))
                    {
                        continue;
                    }

                    // If given path was not found, show error and exit
                    Console.WriteLine($"Error: Couldn't find file: {fileName}");
                    return;
                }
            }
            else
            {
                if (!Directory.Exists(defaultDataFolder))
                {
                    // If no parameters were given and the data folder wasn't found, show error and exit
                    Console.WriteLine("Error: Couldn't find history files.");

                    Console.Write($"Please put the text files into the {defaultDataFolder} directory ");
                    Console.WriteLine("or specify them as the arguments.");

                    return;
                }

                inputFileNames = Directory.GetFiles(defaultDataFolder);
            }

            var inputFiles = inputFileNames.Where(fileName => fileName.EndsWith(".txt")).ToArray();
            if (inputFiles.Length == 0)
            {
                // If there are no text files in the directory, show error and exit
                Console.WriteLine("The given directory is empty. Please provide a directory with the history files.");
                return;
            }

            Console.WriteLine($"[i] {inputFiles.Length} history files found. Parsing messages...");

            var history = History.Parse(inputFiles);

            Console.WriteLine("[i] Messages were parsed successfully. Printing statistics...");

            // Print history statistics
            PrintStatistics(history);

            Console.ReadKey();
        }

        private static void PrintStatistics(History history)
        {
            var textMessages = history.Messages.Where(message => message.Type == MessageTypes.Text).ToArray();
            var words = textMessages.SelectMany(message => message.Value.Split(' ')).ToArray();

            // Print statistics for the whole history
            Console.WriteLine("\nAll-time statistics");

            // Basic history statistics
            PrintBasicStatistics(history, textMessages, words);

            // Dates without messages
            Console.WriteLine("Dates without activity (longer than 7 days)");
            var emptyDatesTable = new ConsoleTable("Missing date", "Timespan");

            foreach (var date in history.MissingDates)
            {
                if ((date.To - date.From).Days > 7)
                {
                    emptyDatesTable.AddRow($"{date.From:d} - {date.To:d}", GetTimeSpanString(date.From, date.To));
                }
            }

            emptyDatesTable.Write(Format.Alternative);

            // Most common words
            PrintMostCommonWords(words);

            // User statistics
            Console.WriteLine("Users by number of words (top 10)");

            var userByWordsTable = new ConsoleTable("#", "Name", "Number of words");
            var userWordCounts = history.GetUserWordCounts(textMessages);
            PrintUserLists(userWordCounts, userByWordsTable);

            Console.WriteLine("Users by number of messages (top 10)");

            var userByMessagesTable = new ConsoleTable("#", "Name", "Number of messages");
            var userMessageCounts = history.GetUserMessageCounts(textMessages);
            PrintUserLists(userMessageCounts, userByMessagesTable);

            // Prompt the user to select a Skype user to get statistics about
            SelectUser(userMessageCounts.Select(userTuple => userTuple.Item1).ToArray());
        }

        private static void PrintBasicStatistics(History history, IReadOnlyList<Message> textMessages, string[] words)
        {
            // First and last message
            var startDate = textMessages[0].SendDate;
            var endDate = textMessages.Last().SendDate;

            var basicStatsTable = new ConsoleTable("First message", startDate.ToString(CultureInfo.CurrentCulture))
                .AddRow("Last Message", endDate);

            // History timespan
            var totalMonths = (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month;
            totalMonths += endDate.Day < startDate.Day ? -1 : 0;

            var years = totalMonths / 12;
            var months = totalMonths % 12;
            var days = endDate.Subtract(startDate.AddMonths(totalMonths)).Days;
            var timeSpan = $"{years} year(s) {months} month(s) {days} day(s)";

            basicStatsTable.AddRow("Timespan", timeSpan);

            // Message statistics
            var emptyDayCount = history.MissingDates.Aggregate(0, (sum, date) => sum + (date.To - date.From).Days);
            var totalDayCount = (endDate - startDate).Days;
            var percentageString = ((double) emptyDayCount / totalDayCount * 100).ToString("N2") + "%";

            var emptyDayText = $"{emptyDayCount} (out of {totalDayCount} days) ({percentageString})";

            // Message types
            var messageTypeCounts = history.Messages.GroupBy(message => message.Type)
                .Select(typeGroup => (typeGroup.Key, typeGroup.Count()))
                .ToDictionary(countTuple => countTuple.Key, countTuple => countTuple.Item2);

            basicStatsTable.AddRow("Number of days without activity", emptyDayText)
                .AddRow("Number of text messages", messageTypeCounts[MessageTypes.Text].ToString("N0"))
                .AddRow("Number of removed messages", messageTypeCounts[MessageTypes.Removed].ToString("N0"))
                .AddRow("Number of status messages", messageTypeCounts[MessageTypes.Status].ToString("N0"))
                .AddRow("Number of files sent", messageTypeCounts[MessageTypes.File].ToString("N0"))
                .AddRow("Number of calls", messageTypeCounts[MessageTypes.Call].ToString("N0"));

            // Average number of messages
            basicStatsTable.AddRow("Average number of messages a day",
                ((double) textMessages.Count / totalDayCount).ToString("N2"));

            // Word and character statistics
            basicStatsTable.AddRow("Number of words", words.Length.ToString("N0"));

            var characterCount = textMessages.Aggregate(0, (current, message) => current + message.Value.Length);
            basicStatsTable.AddRow("Number of characters", characterCount.ToString("N0"));

            basicStatsTable.AddRow("Average message length",
                    $"{(double) words.Length / textMessages.Count:N2} words")
                .AddRow("Average word length", $"{(double) characterCount / words.Length:N2} characters");

            basicStatsTable.Write(Format.Alternative);
        }

        private static string GetTimeSpanString(DateTime startDate, DateTime endDate)
        {
            var totalMonths = (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month;
            totalMonths += endDate.Day < startDate.Day ? -1 : 0;

            var years = totalMonths / 12;
            var months = totalMonths % 12;
            var days = endDate.Subtract(startDate.AddMonths(totalMonths)).Days;

            var timeSpanString = "";
            if (years > 0)
            {
                timeSpanString += $"{years} year(s) ";
            }

            if (months > 0)
            {
                timeSpanString += $"{months} month(s) ";
            }

            if (days > 0)
            {
                timeSpanString += $"{days} day(s)";
            }

            return timeSpanString;
        }

        private static void PrintUserLists(IReadOnlyList<(User, int)> users, ConsoleTable usersTable)
        {
            // Print users in the order of the number of messages they sent
            for (var i = 0; i < (users.Count < 10 ? users.Count : 10); i++)
            {
                var currentNames = users[i].Item1.Names;
                var displayedNames = currentNames.Count > 3 ? currentNames.Take(3) : currentNames;
                var nameText = string.Join(", ", displayedNames);

                if (currentNames.Count > 3)
                {
                    nameText += ", ...";
                }

                usersTable.AddRow(i, nameText, users[i].Item2.ToString("N0"));
            }

            usersTable.Write(Format.Alternative);
        }

        private static void SelectUser(IReadOnlyList<User> users)
        {
            // Always ask for a new user selection until user chooses to exit
            while (true)
            {
                // Show prompt
                Console.Write("Select a user by number to see user-specific statistics ");
                Console.WriteLine("or type \".\" to list all users or \"x\" to exit: ");

                var userSelectionString = Console.ReadLine();
                switch (userSelectionString)
                {
                    case ".":
                    {
                        // If "." is given, print all the names...
                        var allUsersTable = new ConsoleTable("#", "Name", "Number of messages");

                        for (var i = 0; i < users.Count; i++)
                        {
                            var username = string.Join(", ", users[i].Names);
                            var messageCount = users[i].Messages.Count;

                            allUsersTable.AddRow(i, username, messageCount);
                        }

                        allUsersTable.Write(Format.Alternative);

                        // ...then ask for user selection again
                        Console.WriteLine("Select a user by number to see user-specific statistics: ");
                        userSelectionString = Console.ReadLine();

                        break;
                    }

                    case "x":
                    case "exit":
                        // If user chose to exit, exit the program
                        Environment.Exit(0);
                        return;
                }

                // Validate if given string is a number and in the range of number of users
                if (!int.TryParse(userSelectionString, out var userNumber) && userNumber < users.Count)
                {
                    Console.WriteLine("\nError: Please enter a number from the table above.\n");
                }
                else
                {
                    // If it is, print the selected user's statistics
                    PrintUserStatistics(users[userNumber]);
                }
            }
        }

        private static void PrintUserStatistics(User user)
        {
            var messages = user.Messages;

            // Print first and last message
            var userTable = new ConsoleTable("First message", messages[0].Value);
            userTable.AddRow("Last message", messages.Last().Value);

            userTable.Write(Format.Alternative);
        }

        private static void PrintMostCommonWords(IEnumerable<string> words)
        {
            // Regex to trim invalid characters from the words
            const string validCharacters = @"^a-zA-Z0-9-_";
            var trimRegex = new Regex($"^[{validCharacters}]+|[{validCharacters}]+$");

            // Trim the words, convert to lower-case and count their concurrences
            var commonWords = words.Select(word => trimRegex.Replace(word, "").ToLowerInvariant())
                .GroupBy(alphaWord => alphaWord)
                .Select(alphaWord => new {Value = alphaWord.Key, Count = alphaWord.Count()})
                .OrderByDescending(wordInfo => wordInfo.Count)
                .ToArray();

            // Print most common words
            Console.WriteLine("Most common words (except a, az, ez)");
            PrintCommonWordsLongerThan(0, commonWords, new[] {"", "a", "az", "ez"}, 50);

            Console.WriteLine("Most common words longer than 5 characters");
            PrintCommonWordsLongerThan(5, commonWords);

            Console.WriteLine("Most common words longer than 10 characters");
            PrintCommonWordsLongerThan(10, commonWords);
        }

        private static void PrintCommonWordsLongerThan(int lengthLimit, IEnumerable<dynamic> commonWords,
            string[] ignoredWords = null, int displayCount = 10)
        {
            if (ignoredWords == null)
            {
                ignoredWords = new string[] { };
            }

            // Create most common words table with header
            var wordStatsTable = new ConsoleTable("Word", "Number of occurrences");

            // Limit the number of results
            var commonLimitedWords = commonWords.Where(wordInfo =>
                    !ignoredWords.Contains((string) wordInfo.Value) && wordInfo.Value.Length > lengthLimit)
                .Take(displayCount);

            foreach (var commonWord in commonLimitedWords)
            {
                wordStatsTable.AddRow(commonWord.Value, commonWord.Count);
            }

            // Print the table
            wordStatsTable.Write(Format.Alternative);
        }
    }
}
