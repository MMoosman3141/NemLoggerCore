using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NemLoggerCore {
  public class LogFileListener : TraceListener {
    private readonly BlockingCollection<string> _logQueue = new BlockingCollection<string>();
    private readonly Task _writeTask;

    public override bool IsThreadSafe { get; } = true;

    /// <summary>
    /// Should messages writen to the log file include a prefixed date and time value.
    /// </summary>
    public bool IncludeDateTime { get; set; }

    /// <summary>
    /// What is the log file being written to.
    /// This is a read only parameter, and can only be set during construction.
    /// The value of this parameter may be different than the value passed to the constructor if the file specified is already in use.
    /// </summary>
    public string LogFilename { get; private set; }

    /// <summary>
    /// Constructor of the object.
    /// Creates the directory and the file for logging.
    /// If the file already exists, but is not in use, it will be overwritten.
    /// If the file exists, but is in use, a new file will be created with a prefix of a counter value.  i.e. log_2.log, log_3.log etc.
    /// </summary>
    /// <param name="filename">Optional: The full path of the log file.  If not specified the name will be based on the process name and will be written to the CommonApplicatonData folder (i.e. C:\ProgramData)</param>
    /// <param name="includeDateTime">Optional: Specifies if messages should be prepended with the current date and time.</param>
    public LogFileListener(string filename = null, bool includeDateTime = true) {
      NeedIndent = false;
      IndentLevel = 0;
      IndentSize = 2;

      IncludeDateTime = includeDateTime;

      //If the filename is not establish the a default directly and create the directory and the file.
      //If the filename is not null, get the path from the filename.
      LogFilename = filename;
      string dataDir = Path.GetDirectoryName(filename);
      if (string.IsNullOrWhiteSpace(filename)) {
        string processName = Process.GetCurrentProcess().ProcessName;

        string logFileName = $"{processName}.log";
        dataDir = $@"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}\{processName}";

        LogFilename = Path.Combine(dataDir, logFileName);
      }

      //Create the directory if needed.
      if (!Directory.Exists(dataDir)) {
        CreateDirectory(dataDir);
      }

      //If the file exists, confirm that it can be overwritten.  Otherwise generate a similar filename.
      LogFilename = DetermineFileName(dataDir, filename);

      _writeTask = WriteLogDataAsync();
    }

    /// <summary>
    /// Destructor, used to ensure that file handles are properly disposed of.
    /// </summary>
    ~LogFileListener() {
      Dispose();
    }

    /// <summary>
    /// Writes the value of the object's ToString method to the log file.
    /// </summary>
    /// <param name="o">The object to write to the file.</param>
    public override void Write(object o) {
      Write(o.ToString(), "");
    }

    /// <summary>
    /// Writes a category name along with the object's ToString method to the log file.
    /// </summary>
    /// <param name="o">The object to write to the file.</param>
    /// <param name="category">The category to write to the file.</param>
    public override void Write(object o, string category) {
      Write(o.ToString(), category);
    }

    /// <summary>
    /// Writes a string message to the log file.
    /// </summary>
    /// <param name="message">The message to write to the log file.</param>
    public override void Write(string message) {
      Write(message, "");
    }

    /// <summary>
    /// Writes a category along with a message to the log file.
    /// </summary>
    /// <param name="message">The message to write to the log file.</param>
    /// <param name="category">The category to write to the log file.</param>
    public override void Write(string message, string category) {
      //Assemble the message to be logged.
      StringBuilder logMessage = new StringBuilder();

      if (IncludeDateTime) {
        logMessage.Append($"{DateTime.Now} - ");
      }

      if (!string.IsNullOrWhiteSpace(category)) {
        logMessage.Append($"{category.Trim()} - ");
      }

      if (!string.IsNullOrWhiteSpace(message)) {
        logMessage.Append(message);
      }

      //Indent each line of the logMessage according to the indentLevel and indentSize
      if (IndentLevel > 0) {
        string indent = new string(' ', IndentLevel * IndentSize);

        string[] lines = logMessage.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
        for (int index = 0; index < lines.Length; index++) {
          if (!string.IsNullOrWhiteSpace(lines[index])) {
            lines[index] = $"{indent}{lines[index]}";
          }
        }

        logMessage = new StringBuilder(string.Join(Environment.NewLine, lines));
      }

      _logQueue.Add(logMessage.ToString());
    }

    /// <summary>
    /// Writes the result of the object's ToString method to the log file followed by an Environment.NewLine.
    /// </summary>
    /// <param name="o">The object to write to the log file.</param>
    public override void WriteLine(object o) {
      WriteLine(o.ToString(), "");
    }

    /// <summary>
    /// Writes the category along with the result of the object's ToString method to the log file followed by an Environment.NewLine.
    /// </summary>
    /// <param name="o">The object to write to the log file.</param>
    /// <param name="category">The category to write to the log file.</param>
    public override void WriteLine(object o, string category) {
      WriteLine(o.ToString(), category);
    }

    /// <summary>
    /// Writes a string message to the log file followed by an Environment.NewLine 
    /// </summary>
    /// <param name="message">The message to write to the log file.</param>
    public override void WriteLine(string message) {
      WriteLine(message, "");
    }

    /// <summary>
    /// Writes a catgory along with a string message to the log file followed by an Environment.NewLine 
    /// </summary>
    /// <param name="message">The message to write to the log file.</param>
    /// <param name="category">The category to write to the log file.</param>
    public override void WriteLine(string message, string category) {
      string writeMessage = $"{message.Trim()}{Environment.NewLine}";

      Write(writeMessage, category);
    }

    /// <summary>
    /// Overwrites the TraceListener Dispose method.
    /// Cleans up file handles.
    /// 
    /// Note:  Warning CA1063 has been disabled as it is impossible to seal this non-override method.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly")]
    public new void Dispose() {
      _logQueue.CompleteAdding();
      _writeTask.Wait();
      IncludeDateTime = false;
      LogFilename = "";      
      GC.SuppressFinalize(this);
    }

    private async Task WriteLogDataAsync() {
      try {
        using FileStream fileStream = new FileStream(LogFilename, FileMode.Create, FileAccess.Write, FileShare.Read);
        using StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
        while (_logQueue.TryTake(out string logValue)) {
          await streamWriter.WriteAsync(logValue);
        }
      } catch (Exception) {
        throw;
      }
    }

    private void CreateDirectory(string dirPath) {
      //Establish security on the directory so that it is readably by anyone.
      DirectorySecurity dirSec = new DirectorySecurity();
      dirSec.AddAccessRule(
        new FileSystemAccessRule(
          new SecurityIdentifier(WellKnownSidType.WorldSid, null),
          FileSystemRights.FullControl,
          InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
          PropagationFlags.NoPropagateInherit,
          AccessControlType.Allow
        )
      );

      DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
      dirInfo.Create(dirSec);
    }

    private string DetermineFileName(string dataPath, string initialFileName) {
      string fileName = initialFileName;

      if (File.Exists(fileName)) {
        bool cannotOpen;
        int counter = 1;
        string baseFilename = fileName;
        do {
          try {
            using FileStream fileStream = File.Open(fileName, FileMode.Open, FileAccess.Write);
            cannotOpen = false;
          } catch (IOException) {
            counter++;

            string newFilename = $"{Path.GetFileNameWithoutExtension(baseFilename)}_{counter}.log";
            fileName = Path.Combine(dataPath, newFilename);

            if (!File.Exists(LogFilename)) {
              break;
            }

            cannotOpen = true;
          }
        } while (cannotOpen);
      }

      return fileName;
    }
  }
}
