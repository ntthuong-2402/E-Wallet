using System;
using System.Runtime.Serialization;
namespace Friday.BuildingBlocks.Application.Exceptions;

[Serializable]
public class VerifyException : Exception
{
    public VerifyException()
    {
    }
    public VerifyException(string message) : base(message)
    {
    }
    public VerifyException(string message, Exception innerException) : base(message, innerException)
    {
    }
    public VerifyException(string code, string message, string master = "") : base(message)
    {
        ErrorCode = code;
        MasterObject = master;
        Details.Add(new VerifyDetail
        {
            ErrorCode = code,
            ErrorMessage = message,
            MasterObject = master
        });
    }
    /// <summary>
    /// 
    /// </summary>
    public VerifyException(List<VerifyDetail> details)
    {
        Details = details;
    }

    /// <summary>
    /// Initializes a new instance of the Exception class with a specified error message.
    /// </summary>
    /// <param name="messageFormat">The exception message format.</param>
    /// <param name="args">The exception message arguments.</param>
    public VerifyException(string messageFormat, params object[] args) : base(string.Format(messageFormat, args)) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    protected VerifyException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    /// <summary>
    /// Initializes a new instance of the Exception class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public VerifyException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    /// <param name="sourceLine"></param>
    public VerifyException(string message, Exception innerException, string sourceLine) : base(message, innerException)
    {
        SourceLine = sourceLine;
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual string MasterObject { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public virtual List<VerifyDetail> Details { get; set; } = new();
    /// <summary>
    /// 
    /// </summary>
    public class VerifyDetail
    {
        /// <summary>
        /// Master object
        /// </summary>
        public string MasterObject { get; set; }

        /// <summary>
        /// Error code
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; }
    }


}
