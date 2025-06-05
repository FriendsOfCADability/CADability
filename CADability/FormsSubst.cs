using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif


// MouseEventArgs, MouseButtons, Keys, 
// MessageBox, Clipboard, IDataObject, Control.ModifierKeys

/// <summary>
/// Simple substitues for Windows.Forms enum and simple data objects. Copy of the Windows.Forms code.
/// </summary>
namespace CADability.Substitutes
{
    [Flags]
    public enum MouseButtons
    {
        None = 0,
        Left = 1048576,
        Right = 2097152,
        Middle = 4194304,
        XButton1 = 8388608,
        XButton2 = 16777216
    }

    public struct MouseEventArgs
    {
        public MouseButtons Button { get; set; }
        public int Clicks { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Delta { get; set; }
        public Point Location { get; set; }
    }

    public class KeyEventArgs
    {
        public KeyEventArgs(Keys keyData)
        {
            KeyCode = KeyData = keyData;
        }
        public virtual bool Alt { get; }
        public bool Control { get; }
        public bool Handled { get; set; }
        public Keys KeyCode { get; }
        public int KeyValue { get; }
        public Keys KeyData { get; }
        public Keys Modifiers { get; }
        public virtual bool Shift { get; }
        public bool SuppressKeyPress { get; set; }
    }

    public class PaintEventArgs 
    {
        public Rectangle ClipRectangle { get; set; }
        public Graphics Graphics { get; set; }
    }

    public enum CheckState
    {
        Unchecked = 0,
        Checked = 1,
        Indeterminate = 2
    }

    [Flags]
    public enum Keys
    {
        Modifiers = -65536,
        None = 0,
        LButton = 1,
        RButton = 2,
        Cancel = 3,
        MButton = 4,
        XButton1 = 5,
        XButton2 = 6,
        Back = 8,
        Tab = 9,
        LineFeed = 10,
        Clear = 12,
        Return = 13,
        Enter = 13,
        ShiftKey = 16,
        ControlKey = 17,
        Menu = 18,
        Pause = 19,
        Capital = 20,
        CapsLock = 20,
        KanaMode = 21,
        HanguelMode = 21,
        HangulMode = 21,
        JunjaMode = 23,
        FinalMode = 24,
        HanjaMode = 25,
        KanjiMode = 25,
        Escape = 27,
        IMEConvert = 28,
        IMENonconvert = 29,
        IMEAccept = 30,
        IMEAceept = 30,
        IMEModeChange = 31,
        Space = 32,
        Prior = 33,
        PageUp = 33,
        Next = 34,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Select = 41,
        Print = 42,
        Execute = 43,
        Snapshot = 44,
        PrintScreen = 44,
        Insert = 45,
        Delete = 46,
        Help = 47,
        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,
        LWin = 91,
        RWin = 92,
        Apps = 93,
        Sleep = 95,
        NumPad0 = 96,
        NumPad1 = 97,
        NumPad2 = 98,
        NumPad3 = 99,
        NumPad4 = 100,
        NumPad5 = 101,
        NumPad6 = 102,
        NumPad7 = 103,
        NumPad8 = 104,
        NumPad9 = 105,
        Multiply = 106,
        Add = 107,
        Separator = 108,
        Subtract = 109,
        Decimal = 110,
        Divide = 111,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        F13 = 124,
        F14 = 125,
        F15 = 126,
        F16 = 127,
        F17 = 128,
        F18 = 129,
        F19 = 130,
        F20 = 131,
        F21 = 132,
        F22 = 133,
        F23 = 134,
        F24 = 135,
        NumLock = 144,
        Scroll = 145,
        LShiftKey = 160,
        RShiftKey = 161,
        LControlKey = 162,
        RControlKey = 163,
        LMenu = 164,
        RMenu = 165,
        BrowserBack = 166,
        BrowserForward = 167,
        BrowserRefresh = 168,
        BrowserStop = 169,
        BrowserSearch = 170,
        BrowserFavorites = 171,
        BrowserHome = 172,
        VolumeMute = 173,
        VolumeDown = 174,
        VolumeUp = 175,
        MediaNextTrack = 176,
        MediaPreviousTrack = 177,
        MediaStop = 178,
        MediaPlayPause = 179,
        LaunchMail = 180,
        SelectMedia = 181,
        LaunchApplication1 = 182,
        LaunchApplication2 = 183,
        OemSemicolon = 186,
        Oem1 = 186,
        Oemplus = 187,
        Oemcomma = 188,
        OemMinus = 189,
        OemPeriod = 190,
        OemQuestion = 191,
        Oem2 = 191,
        Oemtilde = 192,
        Oem3 = 192,
        OemOpenBrackets = 219,
        Oem4 = 219,
        OemPipe = 220,
        Oem5 = 220,
        OemCloseBrackets = 221,
        Oem6 = 221,
        OemQuotes = 222,
        Oem7 = 222,
        Oem8 = 223,
        OemBackslash = 226,
        Oem102 = 226,
        ProcessKey = 229,
        Packet = 231,
        Attn = 246,
        Crsel = 247,
        Exsel = 248,
        EraseEof = 249,
        Play = 250,
        Zoom = 251,
        NoName = 252,
        Pa1 = 253,
        OemClear = 254,
        KeyCode = 65535,
        Shift = 65536,
        Control = 131072,
        Alt = 262144
    }

    public enum MessageBoxButtons
    {
        OK = 0,
        OKCancel = 1,
        AbortRetryIgnore = 2,
        YesNoCancel = 3,
        YesNo = 4,
        RetryCancel = 5
    }

    public enum DialogResult
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Abort = 3,
        Retry = 4,
        Ignore = 5,
        Yes = 6,
        No = 7
    }

    [Flags]
    public enum DragDropEffects
    {
        Scroll = int.MinValue,
        All = -2147483645,
        None = 0,
        Copy = 1,
        Move = 2,
        Link = 4
    }

    public class DragEventArgs : EventArgs
    {
        public object Data { get; set; } //IDataObject
        public int KeyState { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public DragDropEffects AllowedEffect { get; set; }
        private DragDropEffects effect;
        public DragDropEffects Effect
        {
            get
            {
                return effect;
            }
            set
            {
                effect = value;
                EffectChanged?.Invoke(this);
            }
        }
        public delegate void ChangedDelegate(DragEventArgs e);
        public event ChangedDelegate EffectChanged;
    }

    public enum Shortcut
    {
        None = 0,
        Ins = 45,
        Del = 46,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        ShiftIns = 65581,
        ShiftDel = 65582,
        ShiftF1 = 65648,
        ShiftF2 = 65649,
        ShiftF3 = 65650,
        ShiftF4 = 65651,
        ShiftF5 = 65652,
        ShiftF6 = 65653,
        ShiftF7 = 65654,
        ShiftF8 = 65655,
        ShiftF9 = 65656,
        ShiftF10 = 65657,
        ShiftF11 = 65658,
        ShiftF12 = 65659,
        CtrlIns = 131117,
        CtrlDel = 131118,
        Ctrl0 = 131120,
        Ctrl1 = 131121,
        Ctrl2 = 131122,
        Ctrl3 = 131123,
        Ctrl4 = 131124,
        Ctrl5 = 131125,
        Ctrl6 = 131126,
        Ctrl7 = 131127,
        Ctrl8 = 131128,
        Ctrl9 = 131129,
        CtrlA = 131137,
        CtrlB = 131138,
        CtrlC = 131139,
        CtrlD = 131140,
        CtrlE = 131141,
        CtrlF = 131142,
        CtrlG = 131143,
        CtrlH = 131144,
        CtrlI = 131145,
        CtrlJ = 131146,
        CtrlK = 131147,
        CtrlL = 131148,
        CtrlM = 131149,
        CtrlN = 131150,
        CtrlO = 131151,
        CtrlP = 131152,
        CtrlQ = 131153,
        CtrlR = 131154,
        CtrlS = 131155,
        CtrlT = 131156,
        CtrlU = 131157,
        CtrlV = 131158,
        CtrlW = 131159,
        CtrlX = 131160,
        CtrlY = 131161,
        CtrlZ = 131162,
        CtrlF1 = 131184,
        CtrlF2 = 131185,
        CtrlF3 = 131186,
        CtrlF4 = 131187,
        CtrlF5 = 131188,
        CtrlF6 = 131189,
        CtrlF7 = 131190,
        CtrlF8 = 131191,
        CtrlF9 = 131192,
        CtrlF10 = 131193,
        CtrlF11 = 131194,
        CtrlF12 = 131195,
        CtrlShift0 = 196656,
        CtrlShift1 = 196657,
        CtrlShift2 = 196658,
        CtrlShift3 = 196659,
        CtrlShift4 = 196660,
        CtrlShift5 = 196661,
        CtrlShift6 = 196662,
        CtrlShift7 = 196663,
        CtrlShift8 = 196664,
        CtrlShift9 = 196665,
        CtrlShiftA = 196673,
        CtrlShiftB = 196674,
        CtrlShiftC = 196675,
        CtrlShiftD = 196676,
        CtrlShiftE = 196677,
        CtrlShiftF = 196678,
        CtrlShiftG = 196679,
        CtrlShiftH = 196680,
        CtrlShiftI = 196681,
        CtrlShiftJ = 196682,
        CtrlShiftK = 196683,
        CtrlShiftL = 196684,
        CtrlShiftM = 196685,
        CtrlShiftN = 196686,
        CtrlShiftO = 196687,
        CtrlShiftP = 196688,
        CtrlShiftQ = 196689,
        CtrlShiftR = 196690,
        CtrlShiftS = 196691,
        CtrlShiftT = 196692,
        CtrlShiftU = 196693,
        CtrlShiftV = 196694,
        CtrlShiftW = 196695,
        CtrlShiftX = 196696,
        CtrlShiftY = 196697,
        CtrlShiftZ = 196698,
        CtrlShiftF1 = 196720,
        CtrlShiftF2 = 196721,
        CtrlShiftF3 = 196722,
        CtrlShiftF4 = 196723,
        CtrlShiftF5 = 196724,
        CtrlShiftF6 = 196725,
        CtrlShiftF7 = 196726,
        CtrlShiftF8 = 196727,
        CtrlShiftF9 = 196728,
        CtrlShiftF10 = 196729,
        CtrlShiftF11 = 196730,
        CtrlShiftF12 = 196731,
        AltBksp = 262152,
        AltLeftArrow = 262181,
        AltUpArrow = 262182,
        AltRightArrow = 262183,
        AltDownArrow = 262184,
        Alt0 = 262192,
        Alt1 = 262193,
        Alt2 = 262194,
        Alt3 = 262195,
        Alt4 = 262196,
        Alt5 = 262197,
        Alt6 = 262198,
        Alt7 = 262199,
        Alt8 = 262200,
        Alt9 = 262201,
        AltF1 = 262256,
        AltF2 = 262257,
        AltF3 = 262258,
        AltF4 = 262259,
        AltF5 = 262260,
        AltF6 = 262261,
        AltF7 = 262262,
        AltF8 = 262263,
        AltF9 = 262264,
        AltF10 = 262265,
        AltF11 = 262266,
        AltF12 = 262267
    }

}
