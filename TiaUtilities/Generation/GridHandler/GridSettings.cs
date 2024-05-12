﻿using Newtonsoft.Json;
using System.Drawing;
using System.Windows.Forms;
using TiaUtilities.Generation.Configuration.Utility;
using TiaXmlReader.AutoSave;
using TiaXmlReader.Generation.Configuration;

namespace TiaXmlReader.Generation.GridHandler
{
    public class GridSettings : ISettingsAutoSave
    {
        [JsonProperty] public Color DragSelectedCellBorderColor = Color.DarkGreen;
        [JsonProperty] public Color SingleSelectedCellBorderColor = Color.PaleGreen;
        [JsonProperty] public Color SelectedCellTriangleColor = Color.Green;
        [JsonProperty] public Color PreviewColor = Color.MediumPurple;
        [JsonProperty] public Font GridFont = SystemFonts.DefaultFont;

        public void ShowConfigForm(IWin32Window owner)
        {
            var configForm = new ConfigForm("Preferenze");

            var mainGroup = configForm.Init();
            mainGroup.AddColorPicker().LabelText("Bordo cella selezionata")
                .ApplyColor(this.SingleSelectedCellBorderColor)
                .ColorChanged(color => this.SingleSelectedCellBorderColor = color);

            mainGroup.AddColorPicker().LabelText("Sfondo cella trascinata")
                 .ApplyColor(this.DragSelectedCellBorderColor)
                 .ColorChanged(color => this.DragSelectedCellBorderColor = color);

            mainGroup.AddColorPicker().LabelText("Triangolo trascinamento")
                 .ApplyColor(this.SelectedCellTriangleColor)
                 .ColorChanged(color => this.SelectedCellTriangleColor = color);

            mainGroup.AddColorPicker().LabelText("Anteprima")
                 .ApplyColor(this.PreviewColor)
                 .ColorChanged(color => this.PreviewColor = color);

            configForm.StartShowingAtLocation(Cursor.Position);
            configForm.Init();
            configForm.Show(owner);
        }
    }
}
