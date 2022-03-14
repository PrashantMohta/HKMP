using System;
using System.Threading;

namespace HkmpServer.Command {
    /// <summary>
    /// Input manager for console command-line input.
    /// </summary>
    internal class ConsoleInputManager {
        /// <summary>
        /// Event that is called when input is given by the user.
        /// </summary>
        public event Action<string> ConsoleInputEvent;

        /// <summary>
        /// Object for locking asynchronous access.
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// The currently inputted text in the console.
        /// </summary>
        private string _currentInput;

        /// <inheritdoc cref="_currentInput" />
        private string CurrentInput {
            get {
                lock (_lock) {
                    return _currentInput;
                }
            }
            set {
                lock (_lock) {
                    _currentInput = value;
                }
            }
        }

        /// <summary>
        /// Construct the console input manager by initializing values.
        /// </summary>
        public ConsoleInputManager() {
            CurrentInput = "";
        }

        /// <summary>
        /// Starts the read loop for command-line input.
        /// </summary>
        public void StartReading() {
            new Thread(() => {
                while (true) {
                    var consoleKeyInfo = Console.ReadKey();

                    if (consoleKeyInfo.Key == ConsoleKey.Escape) {
                        CurrentInput = "";
                        continue;
                    }

                    if (consoleKeyInfo.Key == ConsoleKey.Backspace) {
                        if (CurrentInput.Length > 0) {
                            for (var i = 0; i < CurrentInput.Length; i++) {
                                Console.Write(" ");
                            }

                            CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
                        }

                        Console.CursorLeft = 0;
                        Console.Write(CurrentInput);

                        continue;
                    }

                    if (consoleKeyInfo.Key == ConsoleKey.Enter) {
                        Clear();

                        var input = CurrentInput;
                        CurrentInput = "";

                        ConsoleInputEvent?.Invoke(input);

                        continue;
                    }

                    CurrentInput += consoleKeyInfo.KeyChar;

                    Console.CursorLeft = 0;
                    Console.Write(CurrentInput);
                }
                // ReSharper disable once FunctionNeverReturns
            }).Start();
        }

        /// <summary>
        /// Writes a line to the console and restores the current input.
        /// </summary>
        /// <param name="line">The line to write.</param>
        public void WriteLine(string line) {
            if (CurrentInput != "") {
                Clear();
            }
            
            Console.WriteLine(line);
            
            Console.Write(CurrentInput);
        }
        
        /// <summary>
        /// Clears the current input.
        /// </summary>
        private static void Clear() {
            var length = Console.CursorLeft;
            Console.CursorLeft = 0;

            for (var i = 0; i < length; i++) {
                Console.Write(" ");
            }

            Console.CursorLeft = 0;
        }
    }
}