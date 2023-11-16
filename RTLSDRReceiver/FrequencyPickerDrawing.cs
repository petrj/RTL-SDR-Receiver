using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class FrequencyPickerDrawing : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.StrokeColor = Colors.Blue;

            var steps = 6;

            for (var i = 0; i <= steps; i++)
            {
                var x = dirtyRect.Width / steps;
                var y = i % 2 == 0 ? dirtyRect.Height /2 : 0;
                canvas.StrokeSize = i % 2 == 0 ? 6 : 4;
                canvas.DrawLine(x*i, dirtyRect.Height , x*i, y);
            }
        }
    }
}
