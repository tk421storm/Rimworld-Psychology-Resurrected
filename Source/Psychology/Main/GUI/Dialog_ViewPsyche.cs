using UnityEngine;
using Verse;

namespace Psychology;

// Pop-up psyche card, opened from the button next to the info button on the character card.
public class Dialog_ViewPsyche : Window
{
    public Pawn pawn;
    public bool EditAllowedBool;
    public float editWidth;

    public Dialog_ViewPsyche(Pawn editFor, bool editBool = false)
    {
        pawn = editFor;
        EditAllowedBool = editBool;
    }

    public override Vector2 InitialSize
    {
        get
        {
            Rect totalRect = PsycheCardUtility.PsycheRect;
            if (EditAllowedBool)
            {
                editWidth = EditPsycheUtility.CalculateEditWidth(pawn);
                totalRect.width += editWidth;
            }
            return totalRect.size;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Rect oldInRect = inRect;
        GUI.EndGroup();

        bool flag = false;
        if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.Escape))
        {
            flag = true;
            Event.current.Use();
        }
        
        Rect psycheRect = PsycheCardUtility.PsycheRect;
        inRect = psycheRect;
        if (EditAllowedBool)
        {
            inRect.width += editWidth;
        }
        Find.WindowStack.currentlyDrawnWindow.windowRect = inRect;
        Find.WindowStack.currentlyDrawnWindow.windowRect.center = 0.5f * new Vector2(UI.screenWidth, UI.screenHeight);

        GUI.BeginGroup(inRect);
        PsycheCardUtility.DrawPsycheCard(psycheRect, pawn, true);
        if (EditAllowedBool)
        {
            Rect editRect = new Rect(psycheRect.xMax, psycheRect.y, editWidth, psycheRect.height);
            EditPsycheUtility.DrawEditPsyche(editRect, pawn);
            UIAssets.DrawLineVertical(editRect.x, editRect.y, editRect.height, UIAssets.LineColor);
            GUI.color = Color.white;
        }
        GUI.EndGroup();

        if (flag)
        {
            this.Close(true);
        }

        GUI.BeginGroup(oldInRect);

    }
}
