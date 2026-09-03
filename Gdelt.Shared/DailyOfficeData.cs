namespace GdeltSearchUI;

internal enum Office { Morning, Evening }

// Static text for "The Daily Office" account: the opening versicles, the
// traditional 30-day psalm cycle (1662 Book of Common Prayer distribution), a
// rotating set of well-known collects, and the OT/NT book lists that drive the
// continuous lesson crawl. Every source text is public domain (1662/1928 BCP,
// traditional language) — the account never calls an external API.
internal static class DailyOfficeData
{
    public static string OpeningVersicle(Office o) => o == Office.Morning
        ? "O Lord, open thou our lips;\nand our mouth shall shew forth thy praise."
        : "O God, make speed to save us;\nO Lord, make haste to help us.";

    // Psalms appointed for each day of the month, indexed 1..30 (index 0 unused).
    // A 31st day reuses day 30 (the traditional rubric). Pre-formatted for
    // display; Psalm 119 is broken into the sections the BCP appoints.
    public static readonly string[] MorningPsalms =
    [
        "",
        "1–5",   "9–11",  "15–17", "19–21", "24–26",
        "30–31", "35–36", "38–40", "44–46", "50–52",
        "56–58", "62–64", "68",         "71–72", "75–77",
        "79–81", "86–88", "90–92", "95–97", "102–103",
        "105",        "107",        "110–113","116–118","119:33–72",
        "119:105–144", "120–125", "132–135", "139–140", "144–146",
    ];

    public static readonly string[] EveningPsalms =
    [
        "",
        "6–8",   "12–14", "18",         "22–23", "27–29",
        "32–34", "37",         "41–43", "47–49", "53–55",
        "59–61", "65–67", "69–70", "73–74", "78",
        "82–85", "89",         "93–94", "98–101","104",
        "106",        "108–109","114–115","119:1–32","119:73–104",
        "119:145–176", "126–131", "136–138", "141–143", "147–150",
    ];

    public static string PsalmsFor(Office o, DateTime date)
    {
        var day = Math.Min(date.Day, 30);
        return (o == Office.Morning ? MorningPsalms : EveningPsalms)[day];
    }

    // The "Collect of the Day" for Morning Prayer, rotated by day-of-year (~a
    // two-week cycle). Evening Prayer always closes with the fixed Collect for
    // Aid Against All Perils below. Extend this list freely — order is not
    // significant and nothing persists an index into it.
    public static readonly string[] MorningCollects =
    [
        // Collect for Grace
        "O Lord, our heavenly Father, Almighty and everlasting God, who hast safely brought us to the beginning of this day: Defend us in the same with thy mighty power; and grant that this day we fall into no sin, neither run into any kind of danger; but that all our doings, being ordered by thy governance, may be righteous in thy sight; through Jesus Christ our Lord. Amen.",
        // Collect for Peace
        "O God, who art the author of peace and lover of concord, in knowledge of whom standeth our eternal life, whose service is perfect freedom: Defend us thy humble servants in all assaults of our enemies; that we, surely trusting in thy defence, may not fear the power of any adversaries; through the might of Jesus Christ our Lord. Amen.",
        // Collect for Purity
        "Almighty God, unto whom all hearts be open, all desires known, and from whom no secrets are hid: Cleanse the thoughts of our hearts by the inspiration of thy Holy Spirit, that we may perfectly love thee, and worthily magnify thy holy Name; through Christ our Lord. Amen.",
        // Ash Wednesday
        "Grant, we beseech thee, Almighty God, that we, who for our evil deeds do worthily deserve to be punished, by the comfort of thy grace may mercifully be relieved; through our Lord and Saviour Jesus Christ. Amen.",
        // Lord of all power and might
        "Lord of all power and might, who art the author and giver of all good things: Graft in our hearts the love of thy Name, increase in us true religion, nourish us with all goodness, and of thy great mercy keep us in the same; through Jesus Christ our Lord. Amen.",
        // Advent I
        "Almighty God, give us grace that we may cast away the works of darkness, and put upon us the armour of light, now in the time of this mortal life in which thy Son Jesus Christ came to visit us in great humility; that in the last day, when he shall come again in his glorious majesty to judge both the quick and the dead, we may rise to the life immortal; through him who liveth and reigneth with thee and the Holy Ghost, now and ever. Amen.",
        // Christmas Day
        "Almighty God, who hast given us thy only-begotten Son to take our nature upon him, and as at this time to be born of a pure virgin: Grant that we, being regenerate and made thy children by adoption and grace, may daily be renewed by thy Holy Spirit; through the same our Lord Jesus Christ. Amen.",
        // The Epiphany
        "O God, who by the leading of a star didst manifest thy only-begotten Son to the Gentiles: Mercifully grant that we, who know thee now by faith, may after this life have the fruition of thy glorious Godhead; through Jesus Christ our Lord. Amen.",
        // Lent I
        "O Lord, who for our sake didst fast forty days and forty nights: Give us grace to use such abstinence, that, our flesh being subdued to the Spirit, we may ever obey thy godly motions in righteousness and true holiness; through Jesus Christ our Lord. Amen.",
        // Easter Day
        "Almighty God, who through thine only-begotten Son Jesus Christ hast overcome death, and opened unto us the gate of everlasting life: We humbly beseech thee that, as thou dost put into our minds good desires, so by thy continual help we may bring the same to good effect; through Jesus Christ our Lord. Amen.",
        // Whitsunday
        "God, who as at this time didst teach the hearts of thy faithful people by sending to them the light of thy Holy Spirit: Grant us by the same Spirit to have a right judgement in all things, and evermore to rejoice in his holy comfort; through the merits of Christ Jesus our Saviour. Amen.",
        // Trinity Sunday
        "Almighty and everlasting God, who hast given unto us thy servants grace, by the confession of a true faith, to acknowledge the glory of the eternal Trinity: We beseech thee that thou wouldest keep us stedfast in this faith, and evermore defend us from all adversities; through Christ our Lord. Amen.",
        // A Prayer of St. Chrysostom
        "Almighty God, who hast given us grace at this time with one accord to make our common supplications unto thee; and dost promise that when two or three are gathered together in thy Name thou wilt grant their requests: Fulfil now, O Lord, the desires and petitions of thy servants, as may be most expedient for them; granting us in this world knowledge of thy truth, and in the world to come life everlasting. Amen.",
        // Assist us mercifully
        "Assist us mercifully, O Lord, in these our supplications and prayers, and dispose the way of thy servants towards the attainment of everlasting salvation; that among all the changes and chances of this mortal life, they may ever be defended by thy most gracious and ready help; through Jesus Christ our Lord. Amen.",
    ];

    public const string EveningCollect =
        "Lighten our darkness, we beseech thee, O Lord; and by thy great mercy defend us from all perils and dangers of this night; for the love of thy only Son, our Saviour Jesus Christ. Amen.";

    public static string CollectFor(Office o, DateTime date) => o == Office.Evening
        ? EveningCollect
        : MorningCollects[(date.DayOfYear - 1) % MorningCollects.Length];

    // The lesson crawl reads one OT chapter and one NT chapter per office
    // (four chapters a day). Psalms is dropped from the OT course because the
    // 30-day psalter cycle already covers it. Both courses wrap and never end.
    public static readonly IReadOnlyList<BibleBook> OldTestament =
        BibleBooks.All.Take(39).Where(b => b.Name != "Psalms").ToList();

    public static readonly IReadOnlyList<BibleBook> NewTestament =
        BibleBooks.All.Skip(39).ToList();
}
