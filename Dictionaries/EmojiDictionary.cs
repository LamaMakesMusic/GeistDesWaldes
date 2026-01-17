using Discord;

namespace GeistDesWaldes.Dictionaries;

public static class EmojiDictionary
{
    //EMOJIS
    public const string FIRST_PLACE_MEDAL = "\uD83E\uDD47";
    public const string SECOND_PLACE_MEDAL = "\uD83E\uDD48";
    public const string THIRD_PLACE_MEDAL = "\uD83E\uDD49";

    public const string ARROW_DOUBLE_UP = "\u23EB";
    public const string ARROW_DOUBLE_DOWN = "\u23EC";
    public const string ALARM_CLOCK = "\u23F0";
    public const string BELL = "\uD83D\uDD14";
    public const string BIRTHDAY_CAKE = "\uD83C\uDF82";
    public const string BOOKMARK_TABS = " :bookmark_tabs: ";
    public const string CHART = " :bar_chart: ";
    public const string CHECK_MARK = "\u2714";
    public const string CHRISTMAS_TREE = "\uD83C\uDF84";
    public const string CROSS_MARK = "\u274C";
    public const string DATE = "\uD83D\uDCC5";
    public const string EXCLAMATION = "\u2757";
    public const string FALLEN_LEAF = "\uD83C\uDF42";
    public const string FLOPPY_DISC = "\uD83D\uDCBE";
    public const string GHOST = "\uD83D\uDC7B";
    public const string HOURGLASS = "\u23F3";
    public const string INFO = "\u2139";
    public const string LOCKED = "\uD83D\uDD12";
    public const string JACK_O_LANTERN = "\uD83C\uDF83";
    public const string MONEY_BAG = "\uD83D\uDCB0";
    public const string NO_ENTRY_SIGN = "\uD83D\uDEAB";
    public const string NOTES = " :notes: ";
    public const string NUMBER_SIGN = "\u0023";
    public const string NUMBERS = "\uD83D\uDD22";
    public const string OPEN_FOLDER = " :open_file_folder: ";
    public const string PAGER = "\uD83D\uDCDF";
    public const string PENCIL = "\uD83D\uDCDD";
    public const string PLAY_BUTTON = " :play_pause: ";
    public const string REC_BUTTON = " :record_button: ";
    public const string SCROLL = " :scroll: ";
    public const string SNOWMAN = "\u26C4";
    public const string SPEECH_BALLOON = "\uD83D\uDCAC";
    public const string STOP_BUTTON = " :stop_button: ";
    public const string TROPHY = "\uD83C\uDFC6";
    public const string UNLOCKED = "\uD83D\uDD13";
    public const string WAVE = "\uD83D\uDC4B";
    public const string WAVY_DASH = "\u3030";

    // LOGOS
    public const string WIKIPEDIA_LOGO = @"https://upload.wikimedia.org/wikipedia/en/thumb/8/80/Wikipedia-logo-v2.svg/220px-Wikipedia-logo-v2.svg.png";

    public static Emoji GetEmoji(string unicodeString)
    {
        return new Emoji(unicodeString);
    }
}