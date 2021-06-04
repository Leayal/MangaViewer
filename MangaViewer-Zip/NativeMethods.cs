using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using static MangaViewer_Zip.NativeMethods.SafeNativeMethods;

namespace MangaViewer_Zip
{
    static class NativeMethods
    {
        [SuppressUnmanagedCodeSecurity]
        internal static class SafeNativeMethods
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            public static extern int StrCmpLogicalW(string psz1, string psz2);

            internal enum ShowWindowCommands
            {
                /// <summary>
                /// Hides the window and activates another window.
                /// </summary>
                Hide = 0,
                /// <summary>
                /// Activates and displays a window. If the window is minimized or
                /// maximized, the system restores it to its original size and position.
                /// An application should specify this flag when displaying the window
                /// for the first time.
                /// </summary>
                Normal = 1,
                /// <summary>
                /// Activates the window and displays it as a minimized window.
                /// </summary>
                ShowMinimized = 2,
                /// <summary>
                /// Maximizes the specified window.
                /// </summary>
                Maximize = 3, // is this the right value?
                /// <summary>
                /// Activates the window and displays it as a maximized window.
                /// </summary>      
                ShowMaximized = 3,
                /// <summary>
                /// Displays a window in its most recent size and position. This value
                /// is similar to <see cref="Win32.ShowWindowCommand.Normal"/>, except
                /// the window is not activated.
                /// </summary>
                ShowNoActivate = 4,
                /// <summary>
                /// Activates the window and displays it in its current size and position.
                /// </summary>
                Show = 5,
                /// <summary>
                /// Minimizes the specified window and activates the next top-level
                /// window in the Z order.
                /// </summary>
                Minimize = 6,
                /// <summary>
                /// Displays the window as a minimized window. This value is similar to
                /// <see cref="Win32.ShowWindowCommand.ShowMinimized"/>, except the
                /// window is not activated.
                /// </summary>
                ShowMinNoActive = 7,
                /// <summary>
                /// Displays the window in its current size and position. This value is
                /// similar to <see cref="Win32.ShowWindowCommand.Show"/>, except the
                /// window is not activated.
                /// </summary>
                ShowNA = 8,
                /// <summary>
                /// Activates and displays the window. If the window is minimized or
                /// maximized, the system restores it to its original size and position.
                /// An application should specify this flag when restoring a minimized window.
                /// </summary>
                Restore = 9,
                /// <summary>
                /// Sets the show state based on the SW_* value specified in the
                /// STARTUPINFO structure passed to the CreateProcess function by the
                /// program that started the application.
                /// </summary>
                ShowDefault = 10,
                /// <summary>
                ///  <b>Windows 2000/XP:</b> Minimizes a window, even if the thread
                /// that owns the window is not responding. This flag should only be
                /// used when minimizing windows from a different thread.
                /// </summary>
                ForceMinimize = 11
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left, Top, Right, Bottom;

                public RECT(int left, int top, int right, int bottom)
                {
                    Left = left;
                    Top = top;
                    Right = right;
                    Bottom = bottom;
                }

                public RECT(System.Drawing.Rectangle r) : this(r.Left, r.Top, r.Right, r.Bottom) { }

                public int X
                {
                    get { return Left; }
                    set { Right -= (Left - value); Left = value; }
                }

                public int Y
                {
                    get { return Top; }
                    set { Bottom -= (Top - value); Top = value; }
                }

                public int Height
                {
                    get { return Bottom - Top; }
                    set { Bottom = value + Top; }
                }

                public int Width
                {
                    get { return Right - Left; }
                    set { Right = value + Left; }
                }

                public System.Drawing.Point Location
                {
                    get { return new System.Drawing.Point(Left, Top); }
                    set { X = value.X; Y = value.Y; }
                }

                public System.Drawing.Size Size
                {
                    get { return new System.Drawing.Size(Width, Height); }
                    set { Width = value.Width; Height = value.Height; }
                }

                public static implicit operator System.Drawing.Rectangle(RECT r)
                {
                    return new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
                }

                public static implicit operator RECT(System.Drawing.Rectangle r)
                {
                    return new RECT(r);
                }

                public static bool operator ==(RECT r1, RECT r2)
                {
                    return r1.Equals(r2);
                }

                public static bool operator !=(RECT r1, RECT r2)
                {
                    return !r1.Equals(r2);
                }

                public bool Equals(RECT r)
                {
                    return r.Left == Left && r.Top == Top && r.Right == Right && r.Bottom == Bottom;
                }

                public override bool Equals(object obj)
                {
                    if (obj is RECT)
                        return Equals((RECT)obj);
                    else if (obj is System.Drawing.Rectangle)
                        return Equals(new RECT((System.Drawing.Rectangle)obj));
                    return false;
                }

                public override int GetHashCode()
                {
                    return ((System.Drawing.Rectangle)this).GetHashCode();
                }

                public override string ToString()
                {
                    return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{{Left={0},Top={1},Right={2},Bottom={3}}}", Left, Top, Right, Bottom);
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int X;
                public int Y;

                public POINT(int x, int y)
                {
                    this.X = x;
                    this.Y = y;
                }

                public static implicit operator System.Drawing.Point(POINT p)
                {
                    return new System.Drawing.Point(p.X, p.Y);
                }

                public static implicit operator POINT(System.Drawing.Point p)
                {
                    return new POINT(p.X, p.Y);
                }
            }

            /// <summary>
            /// Contains information about the placement of a window on the screen.
            /// </summary>
            [Serializable]
            [StructLayout(LayoutKind.Sequential)]
            internal struct WINDOWPLACEMENT
            {
                /// <summary>
                /// The length of the structure, in bytes. Before calling the GetWindowPlacement or SetWindowPlacement functions, set this member to sizeof(WINDOWPLACEMENT).
                /// <para>
                /// GetWindowPlacement and SetWindowPlacement fail if this member is not set correctly.
                /// </para>
                /// </summary>
                public int Length;

                /// <summary>
                /// Specifies flags that control the position of the minimized window and the method by which the window is restored.
                /// </summary>
                public int Flags;

                /// <summary>
                /// The current show state of the window.
                /// </summary>
                public ShowWindowCommands ShowCmd;

                /// <summary>
                /// The coordinates of the window's upper-left corner when the window is minimized.
                /// </summary>
                public POINT MinPosition;

                /// <summary>
                /// The coordinates of the window's upper-left corner when the window is maximized.
                /// </summary>
                public POINT MaxPosition;

                /// <summary>
                /// The window's coordinates when the window is in the restored position.
                /// </summary>
                public RECT NormalPosition;

                /// <summary>
                /// Gets the default (empty) value.
                /// </summary>
                public static WINDOWPLACEMENT Default
                {
                    get
                    {
                        WINDOWPLACEMENT result = new WINDOWPLACEMENT();
                        result.Length = Marshal.SizeOf(result);
                        return result;
                    }
                }
            }

            /// <summary>
            /// Retrieves the show state and the restored, minimized, and maximized positions of the specified window.
            /// </summary>
            /// <param name="hWnd">
            /// A handle to the window.
            /// </param>
            /// <param name="lpwndpl">
            /// A pointer to the WINDOWPLACEMENT structure that receives the show state and position information.
            /// <para>
            /// Before calling GetWindowPlacement, set the length member to sizeof(WINDOWPLACEMENT). GetWindowPlacement fails if lpwndpl-> length is not set correctly.
            /// </para>
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is nonzero.
            /// <para>
            /// If the function fails, the return value is zero. To get extended error information, call GetLastError.
            /// </para>
            /// </returns>
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

            /// <summary>
            /// Sets the show state and the restored, minimized, and maximized positions of the specified window.
            /// </summary>
            /// <param name="hWnd">
            /// A handle to the window.
            /// </param>
            /// <param name="lpwndpl">
            /// A pointer to a WINDOWPLACEMENT structure that specifies the new show state and window positions.
            /// <para>
            /// Before calling SetWindowPlacement, set the length member of the WINDOWPLACEMENT structure to sizeof(WINDOWPLACEMENT). SetWindowPlacement fails if the length member is not set correctly.
            /// </para>
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is nonzero.
            /// <para>
            /// If the function fails, the return value is zero. To get extended error information, call GetLastError.
            /// </para>
            /// </returns>
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);
        }

        public static unsafe byte[] Serialize(ref WINDOWPLACEMENT placement)
        {
            var leng = placement.Length;
            var buffer = new byte[leng];
            fixed (void* d = &buffer[0])
            {
                fixed (void* s = &placement)
                {
                    Buffer.MemoryCopy(s, d, leng, leng);
                }
            }

            return buffer;
        }

        public static unsafe void Deserialize(byte[] data, ref WINDOWPLACEMENT placement)
        {
            var leng = placement.Length;
            var buffer = new byte[leng];
            fixed (void* s = &buffer[0])
            {
                fixed (void* d = &placement)
                {
                    Buffer.MemoryCopy(s, d, leng, leng);
                }
            }
        }

        public static readonly StringComparer NaturalComparer = new NaturalStringComparer();

        sealed class NaturalStringComparer : StringComparer
        {
            public override int Compare(string x, string y) => SafeNativeMethods.StrCmpLogicalW(x, y);

            public override bool Equals(string x, string y) => string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase);

            public override int GetHashCode(string obj) => obj.GetHashCode();
        }
    }
}
