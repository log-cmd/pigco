namespace pigco_input
{
    // JISキーボード想定
    public enum Keys : ushort
    {
        None = 0x0000,

        Mouse_L = 0x2001,
        Mouse_R = 0x2002,
        Mouse_M = 0x2003,
        Mouse_X1 = 0x2004,
        Mouse_X2 = 0x2005,
        Mouse_WheelUp = 0x2006,
        Mouse_WheelDown = 0x2007,

        A = 0x001E,
        B = 0x0030,
        C = 0x002E,
        D = 0x0020,
        E = 0x0012,
        F = 0x0021,
        G = 0x0022,
        H = 0x0023,
        I = 0x0017,
        J = 0x0024,
        K = 0x0025,
        L = 0x0026,
        M = 0x0032,
        N = 0x0031,
        O = 0x0018,
        P = 0x0019,
        Q = 0x0010,
        R = 0x0013,
        S = 0x001F,
        T = 0x0014,
        U = 0x0016,
        V = 0x002F,
        W = 0x0011,
        X = 0x002D,
        Y = 0x0015,
        Z = 0x002C,

        D0 = 0x000B,
        D1 = 0x0002,
        D2 = 0x0003,
        D3 = 0x0004,
        D4 = 0x0005,
        D5 = 0x0006,
        D6 = 0x0007,
        D7 = 0x0008,
        D8 = 0x0009,
        D9 = 0x000A,

        Minus = 0x000C,
        Caret = 0x000D,          // JIS: 「^」「~」
        Yen = 0x007D,            // JIS: 「¥」
        At = 0x0028,             // JIS: 「@」「`」
        OpenBracket = 0x001A,    // 「[」「{」
        CloseBracket = 0x001B,   // 「]」「}」
        Colon = 0x0027,          // JIS: 「:」「*」
        Semicolon = 0x0029,      // JIS: 「;」「+」
        Comma = 0x0033,
        Period = 0x0034,
        Slash = 0x0035,
        Backslash = 0x0073,      // JIS: 「＼」（ろ）

        LeftShift = 0x002A,
        RightShift = 0x0036,
        LeftCtrl = 0x001D,
        RightCtrl = 0xE01D,
        LeftAlt = 0x0038,
        RightAlt = 0xE038,
        LWin = 0xE05B,
        RWin = 0xE05C,
        Menu = 0xE05D,
        CapsLock = 0x003A,

        Space = 0x0039,
        Enter = 0x001C,
        Escape = 0x0001,
        Backspace = 0x000E,
        Tab = 0x000F,

        Insert = 0xE052,
        Delete = 0xE053,
        Home = 0xE047,
        End = 0xE04F,
        PageUp = 0xE049,
        PageDown = 0xE051,

        UpArrow = 0xE048,
        DownArrow = 0xE050,
        LeftArrow = 0xE04B,
        RightArrow = 0xE04D,

        F1 = 0x003B,
        F2 = 0x003C,
        F3 = 0x003D,
        F4 = 0x003E,
        F5 = 0x003F,
        F6 = 0x0040,
        F7 = 0x0041,
        F8 = 0x0042,
        F9 = 0x0043,
        F10 = 0x0044,
        F11 = 0x0057,
        F12 = 0x0058,

        // JIS
        Henkan = 0x0079,
        Muhenkan = 0x007B,
        KatakanaHiragana = 0x0070,

        NumLock = 0x0045,
        NumpadDivide = 0xE035,
        NumpadMultiply = 0x0037,
        NumpadSubtract = 0x004A,
        NumpadAdd = 0x004E,
        NumpadEnter = 0xE01C,
        NumpadDecimal = 0x0053,
        Numpad0 = 0x0052,
        Numpad1 = 0x004F,
        Numpad2 = 0x0050,
        Numpad3 = 0x0051,
        Numpad4 = 0x004B,
        Numpad5 = 0x004C,
        Numpad6 = 0x004D,
        Numpad7 = 0x0047,
        Numpad8 = 0x0048,
        Numpad9 = 0x0049
    }
}
