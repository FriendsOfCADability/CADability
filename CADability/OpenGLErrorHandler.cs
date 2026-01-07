using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CADability
{
    /// <summary>
    /// Provides robust error checking and logging for OpenGL calls
    /// </summary>
    public static class OpenGLErrorHandler
    {
        /// <summary>
        /// Logging level for OpenGL operations
        /// </summary>
        public enum LogLevel
        {
            None = 0,
            Error = 1,
            Warning = 2,
            Info = 3,
            Debug = 4
        }

        private static LogLevel currentLogLevel = LogLevel.Error;
        private static bool enableErrorChecking = true;

        /// <summary>
        /// Gets or sets the current logging level
        /// </summary>
        public static LogLevel CurrentLogLevel
        {
            get => currentLogLevel;
            set => currentLogLevel = value;
        }

        /// <summary>
        /// Gets or sets whether error checking is enabled
        /// </summary>
        public static bool EnableErrorChecking
        {
            get => enableErrorChecking;
            set => enableErrorChecking = value;
        }

        /// <summary>
        /// Checks for OpenGL errors and logs them if found
        /// </summary>
        /// <param name="operation">Description of the operation being performed</param>
        /// <param name="memberName">Auto-filled by compiler</param>
        /// <param name="sourceFilePath">Auto-filled by compiler</param>
        /// <param name="sourceLineNumber">Auto-filled by compiler</param>
        /// <returns>True if an error was detected, false otherwise</returns>
        public static bool CheckError(
            string operation = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (!enableErrorChecking)
                return false;

            int error = Gl.glGetError();
            if (error == Gl.GL_NO_ERROR)
                return false;

            string errorMessage = GetErrorString(error);
            string location = $"{System.IO.Path.GetFileName(sourceFilePath)}:{sourceLineNumber} in {memberName}";
            
            if (!string.IsNullOrEmpty(operation))
                location = $"{location} ({operation})";

            LogError($"OpenGL Error {error:X}: {errorMessage} at {location}");

            // Handle critical errors
            if (error == Gl.GL_OUT_OF_MEMORY)
            {
                throw new PaintTo3DOutOfMemory();
            }

            return true;
        }

        /// <summary>
        /// Converts an OpenGL error code to a human-readable string
        /// </summary>
        /// <param name="errorCode">The OpenGL error code</param>
        /// <returns>Description of the error</returns>
        public static string GetErrorString(int errorCode)
        {
            switch (errorCode)
            {
                case Gl.GL_NO_ERROR:
                    return "No error";
                case Gl.GL_INVALID_ENUM:
                    return "Invalid enum - An unacceptable value is specified for an enumerated argument";
                case Gl.GL_INVALID_VALUE:
                    return "Invalid value - A numeric argument is out of range";
                case Gl.GL_INVALID_OPERATION:
                    return "Invalid operation - The specified operation is not allowed in the current state";
                case Gl.GL_STACK_OVERFLOW:
                    return "Stack overflow - An attempt has been made to perform an operation that would cause an internal stack to overflow";
                case Gl.GL_STACK_UNDERFLOW:
                    return "Stack underflow - An attempt has been made to perform an operation that would cause an internal stack to underflow";
                case Gl.GL_OUT_OF_MEMORY:
                    return "Out of memory - There is not enough memory left to execute the command";
                default:
                    return $"Unknown error code: {errorCode:X}";
            }
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void LogError(string message)
        {
            if (currentLogLevel >= LogLevel.Error)
            {
                Debug.WriteLine($"[OpenGL ERROR] {message}");
                Trace.WriteLine($"[OpenGL ERROR] {message}");
            }
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            if (currentLogLevel >= LogLevel.Warning)
            {
                Debug.WriteLine($"[OpenGL WARNING] {message}");
                Trace.WriteLine($"[OpenGL WARNING] {message}");
            }
        }

        /// <summary>
        /// Logs an info message
        /// </summary>
        public static void LogInfo(string message)
        {
            if (currentLogLevel >= LogLevel.Info)
            {
                Debug.WriteLine($"[OpenGL INFO] {message}");
                Trace.WriteLine($"[OpenGL INFO] {message}");
            }
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        public static void LogDebug(string message)
        {
            if (currentLogLevel >= LogLevel.Debug)
            {
                Debug.WriteLine($"[OpenGL DEBUG] {message}");
            }
        }

        /// <summary>
        /// Clears any pending OpenGL errors without logging
        /// Useful for initialization or when starting error tracking
        /// </summary>
        public static void ClearErrors()
        {
            while (Gl.glGetError() != Gl.GL_NO_ERROR)
            {
                // Clear all pending errors
            }
        }

        /// <summary>
        /// Executes an action and checks for OpenGL errors afterward
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="operation">Description of the operation</param>
        public static void ExecuteWithErrorCheck(Action action, string operation)
        {
            try
            {
                action();
                CheckError(operation);
            }
            catch (Exception ex)
            {
                LogError($"Exception during {operation}: {ex.Message}");
                throw;
            }
        }
    }
}
