using System;
using System.Windows.Input;

namespace FileSearch
{
    /// <summary>
    /// A command whose sole purpose is to relay its functionality to other objects by invoking delegates
    /// </summary>
    public class RelayCommand(Action<object> execute, Predicate<object>? canExecute = null) : ICommand
    {
        private readonly Action<object> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Predicate<object>? _canExecute = canExecute;

        public RelayCommand(Action<object> execute) : this(execute, null) { }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state
        /// </summary>
        /// <param name="parameter">Data used by the command</param>
        /// <returns>true if this command can be executed; otherwise, false</returns>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        /// <summary>
        /// Defines the method to be called when the command is invoked
        /// </summary>
        /// <param name="parameter">Data used by the command</param>
        public void Execute(object? parameter) => _execute(parameter!);
    }

    /// <summary>
    /// A generic command whose sole purpose is to relay its functionality to other objects by invoking delegates
    /// </summary>
    /// <typeparam name="T">The type of the command parameter</typeparam>
    public class RelayCommand<T>(Action<T> execute, Predicate<T>? canExecute = null) : ICommand
    {
        private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Predicate<T>? _canExecute = canExecute;

        public RelayCommand(Action<T> execute) : this(execute, null) { }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state
        /// </summary>
        /// <param name="parameter">Data used by the command</param>
        /// <returns>true if this command can be executed; otherwise, false</returns>
        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T)parameter!) ?? true;

        /// <summary>
        /// Defines the method to be called when the command is invoked
        /// </summary>
        /// <param name="parameter">Data used by the command</param>
        public void Execute(object? parameter) => _execute((T)parameter!);
    }
}