using System;
using RimWorld;
using Verse;
using UnityEngine;

namespace Psychology;

// The "Psyche" inspector tab on pawns (and their corpses) with a psyche; draws PsycheCardUtility.DrawPsycheCard.
public class ITab_Pawn_Psyche : ITab
{
    public ITab_Pawn_Psyche()
    {
        size = new Vector2(200f, 200f);
        labelKey = "TabPsyche";
        this.tutorTag = "Psyche";
    }

    public override bool IsVisible
    {
        get
        {
            Pawn pawn = IsVisiblePawnHook(PawnToShowInfoAbout);
            return PsycheHelper.HasLatentPsyche(pawn);
        }
    }

    public Pawn PawnToShowInfoAbout
    {
        get
        {
            if (base.SelPawn != null)
            {
                return base.SelPawn;
            }
            if (base.SelThing is Corpse corpse)
            {
                return corpse.InnerPawn;
            }
            throw new InvalidOperationException("Psyche tab found no selected pawn to display.");
        }
    }

    protected override void FillTab()
    {
        // Get pawn
        Pawn pawn = FillTabPawnHook(PawnToShowInfoAbout);
        // Modify pawn hook
        
        // Get total rectangle
        Rect psycheRect = PsycheCardUtility.PsycheRect;
        Rect totalRect = psycheRect;
        Rect editRect = new Rect(0f, 0f, 1f, 1f);
        if (Prefs.DevMode)
        {
            editRect = new Rect(psycheRect.xMax, psycheRect.y, EditPsycheUtility.CalculateEditWidth(pawn), psycheRect.height);
            totalRect.width += editRect.width;
        }
        size = totalRect.size;
        GUI.BeginGroup(totalRect);
        PsycheCardUtility.DrawPsycheCard(psycheRect, pawn, OnWindow: false);
        if (Prefs.DevMode)
        {
            EditPsycheUtility.DrawEditPsyche(editRect, pawn);
            UIAssets.DrawLineVertical(editRect.x, editRect.y, editRect.height, UIAssets.LineColor);
        }
        GUI.EndGroup();
    }

    public Pawn IsVisiblePawnHook(Pawn pawn)
    {
        return pawn;
    }

    public Pawn FillTabPawnHook(Pawn pawn)
    {
        return pawn;
    }
}
