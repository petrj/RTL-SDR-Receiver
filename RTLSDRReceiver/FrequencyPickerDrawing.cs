using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Text;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTLSDRReceiver
{
    public class FrequencyPickerDrawing : BindableObject, IDrawable
    {

        private double _min = 88000;
        private double _max = 106000;
        //private double _freq = 104000;
        private double _bigStep = 1000;
        private double _halfStep = 500;
        private double _smallStep = 100;

        public static BindableProperty RangeProperty = BindableProperty.Create(nameof(Range), typeof(double), typeof(FrequencyPickerDrawing));
        public double Range
        {
            get => (double)GetValue(RangeProperty);
            set => SetValue(RangeProperty, value);
        }

        public static BindableProperty FrequencyKHzProperty = BindableProperty.Create(nameof(FrequencyKHz), typeof(double), typeof(FrequencyPickerDrawing));
        public double FrequencyKHz
        {
            get => (double)GetValue(FrequencyKHzProperty);
            set => SetValue(FrequencyKHzProperty, value);
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var freq = FrequencyKHz;
            var range = Range;

            var start = freq - range / 2.0;
            var startDiffK = start - Math.Floor(start/1000.00)*1000;
            var ratio = range / dirtyRect.Width;
            var drawBottom = dirtyRect.Height - dirtyRect.Height / 5;

            canvas.StrokeSize = 7;
            canvas.StrokeColor = Colors.Red;
            canvas.DrawLine(dirtyRect.Width / 2, drawBottom, dirtyRect.Width / 2, dirtyRect.Height / 10);

            canvas.FontSize = 18;
            canvas.FontColor = Colors.White;

            canvas.StrokeSize = 1;
            canvas.StrokeColor = Colors.LightBlue;

            for (var i = start - range; i <= freq + range / 2 + range; i += _smallStep)
            {
                var x = Convert.ToInt32((i - start - startDiffK) / ratio);
                canvas.DrawLine(x, drawBottom, x, dirtyRect.Height/2);
            }

            canvas.StrokeSize = 2;
            canvas.StrokeColor = Colors.LightBlue;

            for (var i = start - range; i <= freq + range / 2.0 + range; i += _halfStep)
            {
                var x = Convert.ToInt32((i - start - startDiffK) / ratio);
                canvas.DrawLine(x, drawBottom, x, dirtyRect.Height / 2 - dirtyRect.Height / 8);
            }

            canvas.StrokeSize = 2;
            canvas.StrokeColor = Colors.Yellow;

            for (var i = start - range; i <= freq + range / 2.0 + range; i += _bigStep)
            {
                var x = Convert.ToInt32((i - start - startDiffK) / ratio);
                canvas.DrawLine(x, drawBottom, x, dirtyRect.Height / 2 - dirtyRect.Height / 4);

                var attributedText = MarkdownAttributedTextReader.Read(((i - startDiffK) / 1000).ToString("N0"));
                canvas.DrawText(attributedText, x, drawBottom, Convert.ToInt32(_bigStep), dirtyRect.Height / 5);  //  1/5 height
            }
        }
    }
}
