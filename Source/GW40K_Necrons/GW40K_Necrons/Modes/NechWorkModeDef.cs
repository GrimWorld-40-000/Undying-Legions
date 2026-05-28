using UnityEngine;
using Verse;

namespace GW40K_Necrons;

/// <summary>
/// Data-driven work mode definition for Necron pawns.
/// Mirrors vanilla <c>MechWorkModeDef</c> — modes are defined in XML, not hardcoded C# enums.
/// Each pawn type declares its available modes via <see cref="CompProperties_NechWorkMode"/>.
/// </summary>
public class NechWorkModeDef : Def
{
    [NoTranslate]
    public string iconPath;

    public int uiOrder;

    [Unsaved(false)]
    private Texture2D _uiIcon;

    public Texture2D UIIcon
    {
        get
        {
            if (_uiIcon == null && !iconPath.NullOrEmpty())
                _uiIcon = ContentFinder<Texture2D>.Get(iconPath, reportFailure: false) ?? BaseContent.BadTex;
            return _uiIcon ?? BaseContent.BadTex;
        }
    }

    public override void PostLoad()
    {
        base.PostLoad();
        if (!iconPath.NullOrEmpty())
        {
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                _uiIcon = ContentFinder<Texture2D>.Get(iconPath, reportFailure: false) ?? BaseContent.BadTex;
            });
        }
    }
}
