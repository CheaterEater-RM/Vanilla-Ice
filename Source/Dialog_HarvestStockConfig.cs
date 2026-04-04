using System;
using UnityEngine;
using Verse;

namespace VanillaIce
{
    public class Dialog_HarvestStockConfig : Window
    {
        private readonly string titleKey;
        private readonly string descriptionKey;
        private readonly string enabledLabelKey;
        private readonly string enabledTooltipKey;
        private readonly string pauseLabelKey;
        private readonly string resumeLabelKey;
        private readonly Func<int> currentStockGetter;
        private readonly Func<bool> enabledGetter;
        private readonly Action<bool> enabledSetter;
        private readonly Func<int> pauseGetter;
        private readonly Action<int> pauseSetter;
        private readonly Func<int> resumeGetter;
        private readonly Action<int> resumeSetter;

        private string pauseBuffer;
        private string resumeBuffer;

        public override Vector2 InitialSize => new Vector2(420f, 235f);

        public Dialog_HarvestStockConfig(
            string titleKey,
            string descriptionKey,
            string enabledLabelKey,
            string enabledTooltipKey,
            string pauseLabelKey,
            string resumeLabelKey,
            Func<int> currentStockGetter,
            Func<bool> enabledGetter,
            Action<bool> enabledSetter,
            Func<int> pauseGetter,
            Action<int> pauseSetter,
            Func<int> resumeGetter,
            Action<int> resumeSetter)
        {
            this.titleKey = titleKey;
            this.descriptionKey = descriptionKey;
            this.enabledLabelKey = enabledLabelKey;
            this.enabledTooltipKey = enabledTooltipKey;
            this.pauseLabelKey = pauseLabelKey;
            this.resumeLabelKey = resumeLabelKey;
            this.currentStockGetter = currentStockGetter;
            this.enabledGetter = enabledGetter;
            this.enabledSetter = enabledSetter;
            this.pauseGetter = pauseGetter;
            this.pauseSetter = pauseSetter;
            this.resumeGetter = resumeGetter;
            this.resumeSetter = resumeSetter;

            doCloseX = true;
            closeOnClickedOutside = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), titleKey.Translate());

            Text.Font = GameFont.Small;

            Listing_Standard list = new Listing_Standard();
            Rect contentRect = new Rect(inRect.x, inRect.y + 38f, inRect.width, inRect.height - 38f);
            list.Begin(contentRect);

            list.Label(descriptionKey.Translate());
            list.GapLine();

            bool enabled = enabledGetter();
            list.CheckboxLabeled(enabledLabelKey.Translate(), ref enabled, enabledTooltipKey.Translate());
            if (enabled != enabledGetter())
                enabledSetter(enabled);

            list.Label("VanillaIce_MapStockCount".Translate() + ": " + currentStockGetter());
            list.Gap();

            int pauseAt = pauseGetter();
            if (pauseBuffer == null)
                pauseBuffer = pauseAt.ToString();

            list.TextFieldNumericLabeled(pauseLabelKey.Translate(), ref pauseAt, ref pauseBuffer, 0f, 1000000f);
            if (pauseAt != pauseGetter())
            {
                pauseSetter(pauseAt);
                pauseBuffer = pauseGetter().ToString();
                resumeBuffer = resumeGetter().ToString();
            }

            int resumeAt = resumeGetter();
            if (resumeBuffer == null)
                resumeBuffer = resumeAt.ToString();

            list.TextFieldNumericLabeled(resumeLabelKey.Translate(), ref resumeAt, ref resumeBuffer, 0f, 1000000f);
            if (resumeAt != resumeGetter())
            {
                resumeSetter(resumeAt);
                pauseBuffer = pauseGetter().ToString();
                resumeBuffer = resumeGetter().ToString();
            }

            list.End();
        }
    }
}