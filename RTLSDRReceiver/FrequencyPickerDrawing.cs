using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Text;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace RTLSDRReceiver
{
    public class FrequencyPickerDrawing : BindableObject, IDrawable
    {

        private double _min = 88000;
        private double _max = 106000;
        //private double _freq = 104000;
        private double _bigStep = 1000;
        private double _smallStep = 100;

        public static BindableProperty RangeProperty = BindableProperty.Create(nameof(Range), typeof(double), typeof(FrequencyPickerDrawing));
        public double Range
        {
            get => (double)GetValue(RangeProperty);
            set => SetValue(RangeProperty, value);
        }

        public static BindableProperty FrequencyProperty = BindableProperty.Create(nameof(Frequency), typeof(double), typeof(FrequencyPickerDrawing));
        public double Frequency
        {
            get => (double)GetValue(FrequencyProperty);
            set => SetValue(FrequencyProperty, value);
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var freq = Frequency;
            var range = Range;

            canvas.StrokeColor = Colors.Blue;
            canvas.StrokeSize = 4;
            canvas.FontSize = 18;
            canvas.FontColor = Colors.Blue;

            var start = freq - range / 2.0;
            var ratio = range / dirtyRect.Width;

            for (var i = start; i <= freq + range / 2.0; i += _bigStep)
            {
                var x = Convert.ToInt32((i - start) / ratio);
                canvas.DrawLine(x, dirtyRect.Height/2 , x, dirtyRect.Height/2 - dirtyRect.Height / 3);

                var attributedText = MarkdownAttributedTextReader.Read( (i/1000).ToString("N0") );
                canvas.DrawText(attributedText, x, dirtyRect.Height / 2, Convert.ToInt32(_bigStep), dirtyRect.Height / 2);
            }

            canvas.StrokeSize = 2;
            for (var i = start; i <= freq + range / 2; i += _smallStep)
            {
                var x = Convert.ToInt32((i - start) / ratio);
                canvas.DrawLine(x, dirtyRect.Height/2, x, dirtyRect.Height/2 - dirtyRect.Height / 5);
            }

            canvas.StrokeSize = 6;
            canvas.StrokeColor = Colors.Red;
            canvas.DrawLine(dirtyRect.Width/2, dirtyRect.Height/2, dirtyRect.Width / 2, 0);
        }
    }
}
