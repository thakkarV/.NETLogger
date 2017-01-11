using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NETLogger
{
    /// <summary>
    /// Defines the interface for the logger class.
    /// </summary>
    /// <remarks>
    /// This allows for more the developement of a focused, user friendly logger class
    /// </remarks>
    public interface ILogger
    {
        /// <summary>
        /// Singleton class <see cref="Logger"/>'s interface method for logging general debug messages.
        /// </summary>
        /// <param name = "message">Debug message string.</param>
        void debug(string message);

        /// <summary>
        /// Singleton class <see cref="Logger"/>'s interface method for logging warnings and minor errors.
        /// </summary>
        /// <param name = "warning">Warning message string.</param>
        void warning(string warning);

        /// <summary>
        /// Singleton class <see cref="Logger"/>'s interface method for logging exception information.
        /// </summary>
        /// <param name="message">Message accompanying the exception. 
        /// May include information such as function from which the exception  was thrown</param>
        /// <param name = "e">Exception object thrown.</param>
        void exception(string message, Exception e);
    }

    /// <summary>
    /// Singleton Logger class that provides an <see cref="Instance"/> interface 
    /// for other porgrams to write a new <see cref="Log"/> to the default logfile.
    /// <para>
    /// Verbostiy of the logger can be changed by editing the verbosity property in the constructor of this <see cref="Logger"/> class.
    /// Level 3 logs dubug messages, warnings and exceptions.
    /// Level 2 logs warnings and eceptions.
    /// Level 1 only logs exceptions.
    /// </para>
    /// </summary>
    public sealed class Logger : ILogger
    {
        // Fully lazy implementation for 
        // 1) learning purposes and
        // 2) in case log disabling is a feature to be added in the future
        private static readonly Lazy<Logger> L = new Lazy<Logger>(() => new Logger());

        /// <summary>
        /// Interface instance for the singleton <see cref="Logger"/> class.
        /// </summary> 
        public static Logger Instance
        {
            get
            {
                return L.Value;
            }
        }

        private double entryNumber = 0;
        private int verbosity { get; set; }

        // CONSTRUCTOR : DEFAULT
        // set to private so that only one instance is created
        private Logger()
        {
            this.verbosity = 3;
            LogDispatcher.Instance.pushNewLog(new Log(
                Log.LogType.Debug,
                entryNumber++,
                "LOG -- LogFile created with verbosity " + this.verbosity,
                DateTime.Now.ToString("yyyy - mm - dd hh: mm:ss.fff")));
        }

        /// <summary>
        /// Singleton class <see cref="Logger"/>'s interface method for logging general debug messages.
        /// </summary>
        /// <param name = "message">Debug message string.</param>
        public void debug(string message)
        {
            if (this.verbosity > 2)
            {
                LogDispatcher.Instance.pushNewLog(new Log(
                    Log.LogType.Debug,
                    entryNumber++,
                    message,
                    DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss.fff"
                )));
            }
        }

        /// <summary>
        /// Singleton class <see cref="Logger"/>'s interface method for logging warnings and minor errors.
        /// </summary>
        /// <param name = "warning">Warning message string.</param>
        public void warning(string warning)
        {
            if (this.verbosity > 1)
            {
                LogDispatcher.Instance.pushNewLog(new Log(
                    Log.LogType.Warning,
                    entryNumber++,
                    warning,
                    DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss.fff"
                )));
            }
        }

        /// <summary>
        /// Singleton class <see cref="Logger"/>'s interface method for logging exception information.
        /// </summary>
        /// <param name="message">Message accompanying the exception. 
        /// May include information such as function from which the exception  was thrown</param>
        /// <param name = "e">Exception object thrown.</param>
        public void exception(string message, Exception e)
        {
            LogDispatcher.Instance.pushNewLog(new Log(
                Log.LogType.Exception,
                (entryNumber++),
                message,
                DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss.fff"),
                Thread.CurrentThread.ManagedThreadId,
                e
            ));
        }
    }


    /// <summary>
    /// Manages the file writer thread for the <see cref="Logger"/> and provides <see cref="pushNewLog(Log)"/> for it to push new <see cref="Log"/>
    /// objects onto its <see cref="BlockingCollection{Log}"/>.
    /// </summary>
    /// <remarks>
    /// This allows the <see cref="Logger"/> to execute just a few lines of code and quickly return to the main execution thread.
    /// This preserves the responsiveness of the main application while still allowing accurately time stamped <see cref="Log"/>
    /// entries that are written in the exact even when multiple threads are accessing the logger at the same time.
    /// </remarks>
    internal class LogDispatcher : IDisposable
    {
        /// <summary>
        /// Fully lazy implementation of the singleton class <see cref="LogDispatcher"/>; instantiates when it is first accessed.
        /// </summary> 
        private static readonly Lazy<LogDispatcher> LD = new Lazy<LogDispatcher>(() => new LogDispatcher());

        /// <summary>
        /// File stream that the <see cref="LogDispatcher"/> writes to.
        /// </summary>
        private readonly StreamWriter LogFile;

        /// <summary>
        /// Loggers reprent a producer/consumer pattern and therefore 
        /// </summary>
        /// <remarks>
        /// A BlockingCollection was used instead of a ConcurrentQueue since it allows for a consumable enumerator
        /// </remarks>
        private readonly BlockingCollection<Log> logQueue;

        /// <summary>
        /// Background thread that manages the write operations of the <see cref="LogDispatcher"/>.
        /// </summary>
        private Thread loggingThread;

        /// <summary>
        /// Signals the log writer to end logging when set to true, and calls the <see cref="LogDispatcher.Dispose()"/> as a result.
        /// </summary>
        private bool terminate = false;

        /// <summary>
        /// Gettter for the instance of the singelton class <see cref="LogDispatcher"/>.
        /// </summary>
        public static LogDispatcher Instance
        {
            get
            {
                return LD.Value;
            }
        }

        /// <summary>
        /// Constructor for the singleton <see cref="LogDispatcher"/>. Sets the path of the logfile to a folder called "NETLogger" 
        /// within the user's <see cref="Environment.SpecialFolder.LocalApplicationData"/> folder.
        /// </summary>
        private LogDispatcher()
        {
            // initialize the log folder
            string logPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\NETLogger";
            {
                try
                {
                    Directory.CreateDirectory(logPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occured while making the logfile.");
                    Console.WriteLine(e.ToString());
                }
            }

            // now make logfile
            try
            {
                this.LogFile = File.CreateText(logPath + "\\LogFile.txt");
                this.LogFile.AutoFlush = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occured while opening LogFile.txt");
                Console.WriteLine(e.ToString());
            }
            
            // make the blocking collection that stores all queued logs
            this.logQueue = new BlockingCollection<Log>();

            // initialize writer loggingThread to run in the background
            this.loggingThread = new Thread(logWriter);
            this.loggingThread.IsBackground = false;
            this.loggingThread.Start();
            loggingThread.Join();
        }

        /// <summary>
        /// Writes the <see cref="Log"/> from the <see cref="BlockingCollection{Log}"/> to the log file in a background thread using a <see cref="StreamWriter"/> and deletes written entries.
        /// </summary>
        public void logWriter()
        {
            Log writingLog = new Log();
            while (!terminate && logQueue.Count != 0)
            {
                /*
                  waiting.Set();
                  int i = ManualResetEvent.WaitAny(new WaitHandle[] { hasNewLogs, terminate });

                  // check for termination signal
                  if (i == 0) { return; }

                  BlockingCollection<Log> copyLogQueue = new BlockingCollection<Log>();
                  lock(logQueue)
                  {
                      copyLogQueue = logQueue;
                  }

                  hasNewLogs.Reset();
                  waiting.Reset();
              */
                if (this.logQueue.TryTake(out writingLog, -1))
                {
                    switch(writingLog.lType)
                    {
                        case (Log.LogType.Debug) :
                            LogFile.WriteLine(writingLog.entryNumber + " -- DEBUG -- " + writingLog.timeStamp);
                            LogFile.WriteLine("\t" + writingLog.message);
                            break;

                        case (Log.LogType.Warning) :
                            LogFile.WriteLine(writingLog.entryNumber + " -- WARNING -- " + writingLog.timeStamp);
                            LogFile.WriteLine("\t" + writingLog.message);
                            break;

                        case (Log.LogType.Exception) :
                            LogFile.WriteLine(writingLog.entryNumber + " -- EXCEPTION -- " + writingLog.timeStamp);
                            LogFile.WriteLine("\t ============== EXCEPTION INFORMATION FOLLOWS ============== ");
                            LogFile.WriteLine("\t Log Message: " + writingLog.message);
                            LogFile.WriteLine("\t Exception Message: " + writingLog.e.Message);
                            LogFile.WriteLine("\t Exception Thread: " + writingLog.threadId);
                            LogFile.WriteLine("\t Source: " + writingLog.e.ToString());
                            LogFile.WriteLine("\t =============== END EXCEPTION INFORMATION ================== ");
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Pushes a new <see cref="Log"/> to the end of <see cref="BlockingCollection{Log}"/> of <see cref="LogDispatcher"/>.
        /// </summary>
        /// <param name="log"></param>
        public void pushNewLog(Log log)
        {
            logQueue.Add(log);
        }

        /// <summary>
        /// Override of interface memeber <see cref="IDisposable.Dispose"/>. Called by this class upon completion of logging.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dictates the resource management upon distruction of <see cref="LogDispatcher"/>. Makes a final <see cref="Log"/> entry 
        /// indicating end of logfile.
        /// </summary>
        /// <param name="disposing">True when called manually. False when called from the distructor by the <see cref="GC"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            this.logQueue.Add(new Log(
                Log.LogType.Debug,
                -1,
                "Terminating All Logging Procedures",
                DateTime.Now.ToString("yyyy-mm-dd hh:mm:ss.fff")));
            this.logQueue.CompleteAdding();
            this.terminate = true;
            this.loggingThread.Join();
            if (disposing)
            {
                if (logQueue != null)
                {
                    logQueue.Dispose();
                }
                if (LogFile != null)
                {
                    LogFile.Close();
                    LogFile.Dispose();
                }
            }
        }

        /// <summary>
        /// Calls <see cref="LogDispatcher.Dispose(bool)"/> method with argument false to indicate a call by <see cref="GC"/>.
        /// </summary>
        ~LogDispatcher()
        {
            this.Dispose(false);
        }
    }


    /// <summary>
    /// Encapsulates all the information of a log entry. This includes a <see cref="string"/> message, a <see cref="LogType"/>, 
    /// an <see cref="int"/> thread ID number and optionally an <see cref="Exception"/>.
    /// </summary>
    internal class Log
    {
        /// <summary>
        /// Represents an enumerable array containing types of <see cref="Log"/>s that can be generated.
        /// </summary>
        internal enum LogType { Debug, Warning, Exception };

        /// <summary>
        /// Represents the type of log entry.
        /// </summary>
        internal LogType lType { get; private set; }

        /// <summary>
        /// Number of the log entry.
        /// </summary>
        internal double entryNumber { get; private set; }

        /// <summary>
        /// Log message to be written.
        /// </summary>
        internal string message { get; private set; }

        /// <summary>
        /// Time at which the log entry write was requested.
        /// </summary>
        /// <remarks>
        /// Request time may not be the same as the write time as the <see cref="LogDispatcher"/> writes to the LogFile in an asynchronous manner.
        /// </remarks>
        internal string timeStamp { get; private set; }

        /// <summary>
        /// Parent thread that threw the exception.
        /// </summary>
        internal int threadId { get; private set; }

        /// <summary>
        /// Exception object thrown.
        /// </summary>
        internal Exception e { get; private set; }

        /// <summary>
        /// Inputless constructor to be used by <see cref="LogDispatcher.logWriter"/> method to write to logfile.
        /// </summary>
        internal Log()
        {

        }

        /// <summary>
        /// Creates a new log entry without exception. Used for logging debug information and warnings.
        /// </summary>
        /// <param name="lType">Type of log entry.</param>
        /// <param name="entryNumber">Number of the log entry.</param>
        /// <param name="message">Message to be written to the log.</param>
        /// <param name="timeStamp">Time at which the log was written.</param>
        internal Log(LogType lType, double entryNumber, string message, string timeStamp)
        {
            this.lType = lType;
            this.entryNumber = entryNumber;
            this.message = message;
            this.timeStamp = timeStamp;
        }

        /// <summary>
        /// Creates a log entry for exceptions. Used for logging extensive information about runtime errors.
        /// </summary>
        /// <param name="lType">Type of log entry.</param>
        /// <param name="entryNumber">Number of the log.</param>
        /// <param name="message">Message to be written to the log.</param>
        /// <param name="timeStamp">Time at which the log was written.</param>
        /// <param name="threadId">Parent thread from which the exception was thrown.</param>
        /// <param name="e">Exception object thrown.</param>
        internal Log(LogType lType, double entryNumber, string message, string timeStamp, int threadId, Exception e)
        {
            this.lType = lType;
            this.entryNumber = entryNumber;
            this.message = message;
            this.timeStamp = timeStamp;
            this.threadId = threadId;
            this.e = e;
        }
    }
}