#region Using Directives

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using Microsoft.Win32.SafeHandles;
using System.Threading;

#endregion

namespace ReportBackupTool.Loggers
{
    public class ConsoleLogger : IConsoleLogger
    {
        #region Variables

        private static ConsoleLogger instance;
        private LoggerFilterCriteria filterCriteriaObject;
        private bool hasFilterSet = false;
        static IntPtr buffer;
        static bool initialized;
        static bool breakHit;
        private bool isConsoleVisible = true;
        public static event HandlerRoutine Break;
        public HandlerRoutine handleRoutine = null;
        static private SafeFileHandle consoleHandle; 
        [field: NonSerialized]
        private ReaderWriterLock readWriteLock = new ReaderWriterLock();        

        #endregion

        #region Public Instance

        public static ConsoleLogger Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ConsoleLogger();
                }

                return instance;
            }
        }

        #endregion

        #region Properties

        public LoggerFilterCriteria FilterCriteriaObject
        {
            get 
            { 
                return filterCriteriaObject;
            }
            set 
            { 
                filterCriteriaObject = value;
            } 
        }

        public bool HasFilterSet
        {            
            get
            {
                readWriteLock.AcquireReaderLock(100000);
                try
                {
                    return hasFilterSet;
                }
                finally
                {
                    readWriteLock.ReleaseReaderLock();
                }
            }
            set 
            {
                readWriteLock.AcquireWriterLock(100000);
                try
                {
                    hasFilterSet = value; 
                }
                finally
                {
                    readWriteLock.ReleaseWriterLock();
                }
            }
        }

        public bool IsConsoleVisible
        {
            get { return isConsoleVisible; }
            set { isConsoleVisible = value; }
        }

        /// <summary>
        /// Specifies whether the console window should be visible or hidden
        /// </summary>
        public bool Visible
        {
            get
            {
                IntPtr hwnd = LoggerUtils.GetConsoleWindow();
                return hwnd != IntPtr.Zero && LoggerUtils.IsWindowVisible(hwnd);
            }
            set
            {
                //Initialize();
                IntPtr hwnd = LoggerUtils.GetConsoleWindow();
                if (hwnd != IntPtr.Zero)
                    LoggerUtils.ShowWindow(hwnd, value ? LoggerUtils.SW_SHOW : LoggerUtils.SW_HIDE);
            }
        }
        
        /// Gets or sets the title of the console window        
        public string Title
        {
            get
            {
                StringBuilder sb = new StringBuilder(256);
                LoggerUtils.GetConsoleTitle(sb, sb.Capacity);
                return sb.ToString();
            }
            set
            {
                LoggerUtils.SetConsoleTitle(value);
            }
        }

        
        /// Get the HWND of the console window
        public IntPtr Handle
        {
            get
            {
                //Initialize();
                return LoggerUtils.GetConsoleWindow();
            }
        }
        
        //Gets and sets a new parent hwnd to the console window        
        public IntPtr ParentHandle
        {
            get
            {
                IntPtr hwnd = LoggerUtils.GetConsoleWindow();
                return LoggerUtils.GetParent(hwnd);
            }
            set
            {
                IntPtr hwnd = Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                LoggerUtils.SetParent(hwnd, value);
                int style = LoggerUtils.GetWindowLong(hwnd, LoggerUtils.GWL_STYLE);
                if (value == IntPtr.Zero)
                    LoggerUtils.SetWindowLong(hwnd, LoggerUtils.GWL_STYLE, (style & ~LoggerUtils.WS_CHILD) | LoggerUtils.WS_OVERLAPPEDWINDOW);
                else
                    LoggerUtils.SetWindowLong(hwnd, LoggerUtils.GWL_STYLE, (style | LoggerUtils.WS_CHILD) & ~LoggerUtils.WS_OVERLAPPEDWINDOW);
                LoggerUtils.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, LoggerUtils.SWP_NOSIZE | LoggerUtils.SWP_NOZORDER | LoggerUtils.SWP_NOACTIVATE);
            }
        }
        
        //Get the current Win32 buffer handle
        public IntPtr Buffer
        {
            get
            {
                if (!initialized) Initialize();
                return buffer;
            }
        }        
                
        //Get the current position of the cursor      
        public Coord CursorPosition
        {
            get 
            {
                return Info.CursorPosition;
            }
            set
            {
                Initialize();
                LoggerUtils.SetConsoleCursorPosition(buffer, new Coord());
            }
        }
        
        //Returns a coordinates of visible window of the buffer
        public SmallRect ScreenSize
        {
            get { return Info.Window; }
        }
        
        //Returns the size of buffer
        public Coord BufferSize
        {
            get { return Info.Size; }
        }
        
        //Returns the maximum size of the screen given the desktop dimensions
        public Coord MaximumScreenSize
        {
            get { return Info.MaximumWindowSize; }
        }
        
        //Returns various information about the screen buffer        
        public ConsoleScreenBufferInfo Info
        {
            get
            {
                ConsoleScreenBufferInfo info = new ConsoleScreenBufferInfo();
                IntPtr buffer = Buffer;
                if (buffer != IntPtr.Zero)
                    LoggerUtils.GetConsoleScreenBufferInfo(buffer, out info);
                return info;
            }
        }
        
        //Gets or sets the current color and attributes of text        
        public ConsoleColor Color
        {
            get
            {
                return Info.Attributes;              
            }
            set
            {
                IntPtr buffer = Buffer;
                if (buffer != IntPtr.Zero)
                {
                    LoggerUtils.SetConsoleTextAttribute(buffer, value);
                }               
            }
        }

        #endregion

        #region Public Static Properties

        /// Returns true if Ctrl-C or Ctrl-Break was hit since the last time this property
        /// was called. The value of this property is set to false after each request.        
        public static bool CtrlBreakPressed
        {
            get
            {
                bool value = breakHit;
                breakHit = false;
                return value;
            }
        }        

        #endregion

        #region Constructors

        public ConsoleLogger()
        {
            
        }

        #endregion

        #region Public Static Methods

        public void Initialize()
        {
            if (initialized)
                return;

            IntPtr hwnd = LoggerUtils.GetConsoleWindow();
            initialized = true;            
            // Console app
            if (hwnd != IntPtr.Zero)
            {
                buffer = LoggerUtils.GetStdHandle(LoggerUtils.STD_OUTPUT_HANDLE);
                return;
            }

            // Windows app
            bool success = LoggerUtils.AllocConsole();            

            handleRoutine = new HandlerRoutine(Handler);
            LoggerUtils.SetConsoleCtrlHandler(handleRoutine, true);

            this.Visible = false;
            
            if (!success)
                return;

            buffer = LoggerUtils.CreateConsoleScreenBuffer(LoggerUtils.GENERIC_READ | LoggerUtils.GENERIC_WRITE,
                LoggerUtils.FILE_SHARE_READ | LoggerUtils.FILE_SHARE_WRITE, IntPtr.Zero, LoggerUtils.CONSOLE_TEXTMODE_BUFFER, IntPtr.Zero);

            bool result = LoggerUtils.SetConsoleActiveScreenBuffer(buffer);

            //Set console output buffer size
            IntPtr handle = LoggerUtils.CreateFile(
                                 "CONOUT$",                                    // name
                                 LoggerUtils.GENERIC_WRITE | LoggerUtils.GENERIC_READ,         // desired access
                                 LoggerUtils.FILE_SHARE_WRITE | LoggerUtils.FILE_SHARE_READ,   // share access
                                 null,                                         // no security attributes
                                 LoggerUtils.OPEN_EXISTING,                            // device already exists
                                 0,                                            // no flags or attributes
                                 IntPtr.Zero);                                // no template file.

            consoleHandle = new SafeFileHandle(handle, true);
            const UInt16 conWidth = 256;
            const UInt16 conHeight = 2500;
            Coord dwSize = new Coord(conWidth, conHeight);
            LoggerUtils.SetConsoleScreenBufferSize(consoleHandle.DangerousGetHandle(), dwSize);

            LoggerUtils.SetStdHandle(LoggerUtils.STD_OUTPUT_HANDLE, buffer);
            LoggerUtils.SetStdHandle(LoggerUtils.STD_ERROR_HANDLE, buffer);

            Title = "Logger Console";

            Stream s = Console.OpenStandardInput(LoggerUtils._DefaultConsoleBufferSize);
            StreamReader reader = null;
            if (s == Stream.Null)
                reader = StreamReader.Null;
            else
                reader = new StreamReader(s, Encoding.GetEncoding(LoggerUtils.GetConsoleCP()),
                    false, LoggerUtils._DefaultConsoleBufferSize);               

            Console.SetIn(reader);

            // Set up Console.Out
            StreamWriter writer = null;
            s = Console.OpenStandardOutput(LoggerUtils._DefaultConsoleBufferSize);
            if (s == Stream.Null)
                writer = StreamWriter.Null;
            else
            {
                writer = new StreamWriter(s, Encoding.GetEncoding(LoggerUtils.GetConsoleOutputCP()),
                    LoggerUtils._DefaultConsoleBufferSize);
                writer.AutoFlush = true;
            }

            Console.SetOut(writer);

            s = Console.OpenStandardError(LoggerUtils._DefaultConsoleBufferSize);
            if (s == Stream.Null)
                writer = StreamWriter.Null;
            else
            {
                writer = new StreamWriter(s, Encoding.GetEncoding(LoggerUtils.GetConsoleOutputCP()),
                    LoggerUtils._DefaultConsoleBufferSize);
                writer.AutoFlush = true;
            }           

            Console.SetError(writer);

            //
            // Disable Close as this would close the main app also
            //
            hwnd = LoggerUtils.GetConsoleWindow();
            IntPtr hMenu = LoggerUtils.GetSystemMenu(hwnd, false);
            LoggerUtils.DeleteMenu(hMenu, LoggerUtils.SC_CLOSE, LoggerUtils.MF_BYCOMMAND);
            NativeMethods.SetWindowPos( hwnd, new IntPtr( -1 ), 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE );
        }

        #region Commented - Reconsider Later for Disable Close Button

        public void DisableCloseButton()
        {
            //IntPtr conHandle = Handle;
            //IntPtr consoleMenu = LoggerUtils.GetSystemMenu(conHandle, false);
            //MENUITEMINFO menuItemInfo = new MENUITEMINFO();
            //menuItemInfo.cbSize = Marshal.SizeOf(menuItemInfo);
            //menuItemInfo.dwTypeData = null;
            //menuItemInfo.cch = 100;
            //menuItemInfo.fMask = 0x10;
            //menuItemInfo.fType = 0;
            //menuItemInfo.wID = LoggerUtils.SC_CLOSE;

            //LoggerUtils.SetMenuItemInfo(consoleMenu, menuItemInfo.wID, false, ref menuItemInfo);

            //menuItemInfo.fMask = MF.DISABLED;

            //LoggerUtils.SetMenuItemInfo(consoleMenu, menuItemInfo.wID, false, ref menuItemInfo);
            
        }

        public void RemoveCloseButton(IntPtr conHandle)
        {
            //int n;
            //int disable = 2;
            //int remove = 1024;
           
            //IntPtr consoleMenu = LoggerUtils.GetSystemMenu(conHandle, false);
            //if (consoleMenu != IntPtr.Zero)
            //{
            //    n = LoggerUtils.GetMenuItemCount(consoleMenu);
            //    if (n > 0)
            //    {
            //        //Removes the actual Close button
            //        bool res = LoggerUtils.RemoveMenu(consoleMenu, (uint)(n - 1), disable | remove);
            //        //bool res = LoggerUtils.RemoveMenu(consoleMenu, LoggerUtils.MF_REMOVE, LoggerUtils.MF_BYPOSITION);
            //        //Removes the seperator between the Close button and the Maximize button
            //        LoggerUtils.RemoveMenu(consoleMenu, (uint)(n - 2), LoggerUtils.MF_BYPOSITION | LoggerUtils.MF_REMOVE);
            //        LoggerUtils.DrawMenuBar(conHandle);
            //    }
            //}
        }

        #endregion

        //Produces a simple beep.        
        public void Beep()
        {
            LoggerUtils.MessageBeep(-1);
        }

        /// <summary>
        /// Flashes the console window
        /// </summary>
        /// <param name="once">if off, flashes repeated until the user makes the console foreground</param>
        public void Flash(bool once)
        {
            IntPtr hwnd = LoggerUtils.GetConsoleWindow();
            if (hwnd == IntPtr.Zero)
                return;

            int style = LoggerUtils.GetWindowLong(hwnd, LoggerUtils.GWL_STYLE);
            if ((style & LoggerUtils.WS_CAPTION) == 0)
                return;

            FlashWInfo info = new FlashWInfo();
            info.Size = Marshal.SizeOf(typeof(FlashWInfo));
            info.Flags = LoggerUtils.FLASHW_ALL;
            if (!once) info.Flags |= LoggerUtils.FLASHW_TIMERNOFG;
            LoggerUtils.FlashWindowEx(ref info);
        }

        /// <summary>
        /// Clear the console window
        /// </summary>
        public void Clear()
        {
            Initialize();
            ConsoleScreenBufferInfo info;
            int writtenChars;
            LoggerUtils.GetConsoleScreenBufferInfo(buffer, out info);
            LoggerUtils.FillConsoleOutputCharacter(buffer, ' ', info.Size.x * info.Size.y, new Coord(), out writtenChars);
            CursorPosition = new Coord();
        }

        public Boolean Handler(CtrlTypes CtrlType)
        {   // A switch to handle the event type.
            string message = string.Empty;
            switch (CtrlType)
            {
                case CtrlTypes.CTRL_C_EVENT:
                    message = "A CTRL_C_EVENT was raised by the user.";
                    HideConsole();
                    break;
                case CtrlTypes.CTRL_BREAK_EVENT:
                    message = "A CTRL_BREAK_EVENT was raised by the user.";
                    break;
                case CtrlTypes.CTRL_CLOSE_EVENT:
                    message = "A CTRL_CLOSE_EVENT was raised by the user.";
                    CloseConsole();
                    break;
                case CtrlTypes.CTRL_LOGOFF_EVENT:
                    message = "A CTRL_LOGOFF_EVENT was raised by the user.";
                    break;
                case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                    message = "A CTRL_SHUTDOWN_EVENT was raised by the user.";
                    break;
            }
            return true;
        }

        public void StartConsoleApplication()
        {
            Initialize();
            ShowConsole();
            ParentHandle = new IntPtr();
        }

        public void ShowConsole()
        {
            this.Visible = true;
            isConsoleVisible = true; 
        }

        public void CloseConsole()
        {
            isConsoleVisible = false; 
        }

        public void HideConsole()
        {            
            isConsoleVisible = false;
            LoggerUtils.FreeConsole();
            initialized = false;
        }

        #endregion

        #region Public Methods
        
        public void ResetFilter()
        {
            FilterCriteriaObject = null;
            HasFilterSet = false;
        }

        #endregion        

        #region Private Static Methods

        private static bool HandleBreak(CtrlTypes ctrlTypes)
        {
            breakHit = true;
            if (Break != null)
                Break(ctrlTypes);

            return true;
        }

        #endregion

        #region Helper Methods

        private void InternalWrite(string message, RequestResponseType requestResponse, MessageFilterType msgFilterType, MessageFilterLevel msgFilterLevel, string symbolOrMarket, LogEntryType logType, ConsoleColor color)
        {
            string messageToLog = string.Empty;
            //string logEntryTypeString = String.Empty;

            //if (logType == LogEntryType.Error || logType == LogEntryType.Fatal)
            //    logEntryTypeString = "ERROR";
            //else if (logType == LogEntryType.Warning)
            //    logEntryTypeString = "WARNING";

            StringBuilder sb = new StringBuilder();
            sb.Append( DateTime.Now.ToString( "HH:mm:ss ffff" ) );
            //sb.Append(" : [");
            sb.Append( " : " );
            //sb.Append(category);

            //if (!String.IsNullOrEmpty(logEntryTypeString))
            //{
            //    sb.Append(":");
            //    sb.Append(logEntryTypeString);
            //}

            //sb.Append("] ");
            sb.Append(message);
            sb.Append(" : ");
            sb.Append(requestResponse);
            sb.Append(" : ");
            sb.Append(msgFilterType);
            sb.Append(" : ");
            sb.Append(msgFilterLevel);
            sb.Append(" : ");
            sb.Append(symbolOrMarket);
            sb.Append(" : ");
            sb.Append(logType);
            sb.Append("\n");
            messageToLog = sb.ToString();

            if (color != ConsoleColor.Normal)
            {
                readWriteLock.AcquireWriterLock(1000000);
                try
                {
                    ConsoleColor oldColor = Color;
                    Color = color;
                    Console.Write(messageToLog);
                    Color = oldColor;
                }
                finally
                {
                    readWriteLock.ReleaseWriterLock();
                }
            }
            else
            {
                Console.WriteLine(messageToLog);
            }
        }

        private void InternalWrite(DateTime logTime,string message, string category, LogEntryType logType, ConsoleColor color)
        {
            string messageToLog = string.Empty;
         

            StringBuilder sb = new StringBuilder();
           
            if (logTime != null && logTime != DateTime.MinValue)
            {
                sb.Append(logTime.ToString("HH:mm:ss ffff"));
                sb.Append(" [ SERVER TIME ] ");
            }
            else
            {
                sb.Append(DateTime.Now.ToString("HH:mm:ss ffff"));

            }
            sb.Append(" : ");
        
            sb.Append(message);
            sb.Append("\n");
            messageToLog = sb.ToString();

            if (color != ConsoleColor.Normal)
            {
                readWriteLock.AcquireWriterLock(1000000);
                try
                {
                    ConsoleColor oldColor = Color;
                    Color = color;
                    Console.Write(messageToLog);
                    Color = oldColor;
                }
                finally
                {
                    readWriteLock.ReleaseWriterLock();
                }
            }
            else
            {
                Console.WriteLine(messageToLog);
            }
        }

        private bool ApplyFilter(string message, string category, LogEntryType logType)
        {
            if (FilterCriteriaObject != null)
            {
                string searchString = FilterCriteriaObject.SearchString;
                string searchCategory = FilterCriteriaObject.LogCategory;
                LogEntryType searchType = FilterCriteriaObject.LogType;
                bool isContainInString = false;

                if (FilterCriteriaObject.IsANDOperator)
                {
                    if (!string.IsNullOrEmpty(searchString) && !string.IsNullOrEmpty(message))
                    {
                        string lowersearchstring = searchString.ToLower();
                        string lowermessage = message.ToLower();
                        if (lowermessage.Contains(lowersearchstring))
                        {
                            isContainInString = true;
                        }
                        else
                        {
                            isContainInString = false;
                        }
                    }
                    else
                    {
                        isContainInString = false; 
                    }
                    if (FilterCriteriaObject.IsCategorySpecified)
                    {
                        if (!string.IsNullOrEmpty(searchCategory) && !string.IsNullOrEmpty(category))
                        {
                            if (searchCategory == category)
                            {
                                isContainInString = true;
                            }
                            else
                            {
                                isContainInString = false;
                            }
                        }
                        else
                        {
                            isContainInString = false;
                        }
                    }
                    if (FilterCriteriaObject.IsLogTypeSpecified)
                    {
                        if (searchType != LogEntryType.Debug)
                        {
                            if (searchType == logType)
                            {
                                isContainInString = true;
                            }
                            else
                            {
                                isContainInString = false;
                            }
                        }
                        else
                        {
                            isContainInString = false; 
                        }
                    }
                    return isContainInString;
                }
                else
                {
                    if (!string.IsNullOrEmpty(searchString) && !string.IsNullOrEmpty(message))
                    {
                        if (message.Contains(searchString))
                        {
                            return true;
                        }                        
                    }
                    if (!string.IsNullOrEmpty(searchCategory) && !string.IsNullOrEmpty(category))
                    {
                        if (searchCategory == category)
                        {
                            return true;
                        }
                    }
                    if (searchType != LogEntryType.Debug)
                    {
                        if (searchType == logType)
                        {
                            return true;
                        }
                    }
                }
                return isContainInString;
            }
            else
            {
                return false;
            }            
        }

        #endregion

        #region IConsoleLogger Members

        public void Write(string message, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                if (ApplyFilter(message, null, LogEntryType.Debug))
                {
                    InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
                }
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }

        public void Write(string message, RequestResponseType requestResponse, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                //if (ApplyFilter(message, category, LogEntryType.Debug))
                //{
                InternalWrite(message, requestResponse, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
               // }
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }

        public void Write(string message, MessageFilterType filterType, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                InternalWrite(message, RequestResponseType.All, filterType, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }

        public void Write(string message, RequestResponseType requestResponse, MessageFilterLevel filterLevel, string symbolOrMarket, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                InternalWrite(message, requestResponse, MessageFilterType.All, filterLevel, symbolOrMarket, LogEntryType.Info, textColor);
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }

        public void Write(string message, MessageFilterType filterType, RequestResponseType requestResponse, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                InternalWrite(message, requestResponse, filterType, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }

        public void Write(string message, MessageFilterType filterType, RequestResponseType requestResponse, LogEntryType logType, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                InternalWrite(message, requestResponse, filterType, MessageFilterLevel.None, null, logType, textColor);
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }

        public void Write(string message, MessageFilterType filterType, RequestResponseType requestResponse, MessageFilterLevel filterLevel, string symbolOrMarket, LogEntryType logType, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                InternalWrite(message, requestResponse, filterType, filterLevel, symbolOrMarket, logType, textColor);
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }       

        public void Write(string message, MessageFilterLevel filterLevel, string symbolOrMarket, LogEntryType logType, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, filterLevel, symbolOrMarket, logType, textColor);
            }
            else
            {
                InternalWrite(message, RequestResponseType.All, MessageFilterType.All, MessageFilterLevel.None, null, LogEntryType.Info, textColor);
            }
        }

       // new overloaded time methods
        public void Write(DateTime logTime, string message)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                if (ApplyFilter(message, null, LogEntryType.Debug))
                {
                    InternalWrite(logTime,message, null, LogEntryType.Debug, ConsoleColor.Normal);
                }
            }
            else
            {
                InternalWrite(logTime,message, null, LogEntryType.Debug, ConsoleColor.Normal);
            }
        }
       
        public void Write(DateTime logTime , string message, LogEntryType logType, ConsoleColor textColor)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                if (ApplyFilter(message, null, logType))
                {
                    InternalWrite(logTime,message, null, logType, textColor);
                }
            }
            else
            {
                InternalWrite(logTime,message, null, logType, textColor);
            }
        }

        public void Write(DateTime logTime,  string message, LogEntryType logType)
        {
            if (!isConsoleVisible)
            {
                return;
            }

            if (HasFilterSet)
            {
                if (ApplyFilter(message, null, logType))
                {
                    InternalWrite(logTime,message, null, logType, ConsoleColor.Normal);
                }
            }
            else
            {
                InternalWrite(logTime,message, null, logType, ConsoleColor.Normal);
            }
        }


#endregion
    }
}
