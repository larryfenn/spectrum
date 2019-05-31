﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Spectrum.Base;
using System.Threading;
using System.Windows.Threading;
using System.Diagnostics;
using Spectrum.LEDs;

namespace Spectrum {

  public partial class DomeSimulatorWindow : Window {

    private static Tuple<int, int> GetPoint(int strutIndex, int point) {
      var p = StrutLayoutFactory.GetProjectedPoint(strutIndex, point);
      int index = StrutLayoutFactory.lines[strutIndex, point];
      return new Tuple<int, int>(
        (int)(p.Item1 * 690) + 10,
        (int)(p.Item2 * 690) + 10
      );
    }

    private readonly Configuration config;
    private readonly WriteableBitmap bitmap;
    private Int32Rect rect;
    private readonly byte[] pixels;
    private bool keyMode;
    private readonly Label[] strutLabels;

    public DomeSimulatorWindow(Configuration config) {
      this.InitializeComponent();
      this.config = config;

      this.rect = new Int32Rect(0, 0, 750, 750);
      this.bitmap = new WriteableBitmap(
        this.rect.Width,
        this.rect.Height,
        96,
        96,
        PixelFormats.Bgra32,
        null
      );
      this.pixels = new byte[this.rect.Width * this.rect.Height * 4];
      for (int x = 0; x < this.rect.Width; x++) {
        for (int y = 0; y < this.rect.Height; y++) {
          this.SetPixelColor(this.pixels, x, y, (uint)0xFF000000);
        }
      }
      RenderOptions.SetBitmapScalingMode(
        this.image,
        BitmapScalingMode.NearestNeighbor
      );
      RenderOptions.SetEdgeMode(
        this.image,
        EdgeMode.Aliased
      );

      this.strutLabels = new Label[LEDDomeOutput.GetNumStruts()];
      var brush = new SolidColorBrush(Colors.White);
      for (int i = 0; i < LEDDomeOutput.GetNumStruts(); i++) {
        var pt1 = GetPoint(i, 0);
        var pt2 = GetPoint(i, 1);
        var centerX = (pt1.Item1 + pt2.Item1) / 2;
        var centerY = (pt1.Item2 + pt2.Item2) / 2;
        Label label = new Label();
        label.Content = i;
        label.FontSize = 12;
        label.Visibility = Visibility.Collapsed;
        label.Foreground = brush;
        label.Margin = new Thickness(
          centerX - 10,
          centerY - 10,
          0,
          0
        );
        label.MouseDown += StrutLabelClicked;
        this.strutLabels[i] = label;
        this.canvas.Children.Add(label);
      }
    }

    private void WindowLoaded(object sender, RoutedEventArgs e) {
      this.Draw();
      DispatcherTimer timer = new DispatcherTimer();
      timer.Tick += Update;
      timer.Interval = new TimeSpan(100000); // every 10 milliseconds
      timer.Start();
    }

    private void Draw() {
      uint color = (uint)SimulatorUtils.GetComputerColor(0x000000)
        | (uint)0xFF000000;
      for (int i = 0; i < LEDDomeOutput.GetNumStruts(); i++) {
        var pt1 = GetPoint(i, 0);
        var pt2 = GetPoint(i, 1);
        int numLEDs = LEDDomeOutput.GetNumLEDs(i);
        double deltaX = (pt1.Item1 - pt2.Item1) / (numLEDs + 2.0);
        double deltaY = (pt1.Item2 - pt2.Item2) / (numLEDs + 2.0);
        for (int j = 0; j < numLEDs; j++) {
          int x = pt1.Item1 - (int)(deltaX * (j + 1));
          int y = pt1.Item2 - (int)(deltaY * (j + 1));
          this.SetPixelColor(this.pixels, x, y, color);
        }
      }
      this.bitmap.WritePixels(this.rect, this.pixels, this.rect.Width * 4, 0);
      this.image.Source = this.bitmap;
    }

    private void SetPixelColor(byte[] pixels, int x, int y, uint color) {
      int pos = (y * this.rect.Width + x) * 4;
      pixels[pos] = (byte)color;
      pixels[pos + 1] = (byte)(color >> 8);
      pixels[pos + 2] = (byte)(color >> 16);
      pixels[pos + 3] = (byte)(color >> 24);
    }

    private void Update(object sender, EventArgs e) {
      int queueLength = this.config.domeCommandQueue.Count;
      if (queueLength == 0) {
        return;
      }

      //Stopwatch stopwatch = Stopwatch.StartNew();

      bool shouldRedraw = false;
      for (int k = 0; k < queueLength; k++) {
        DomeLEDCommand command;
        bool result =
          this.config.domeCommandQueue.TryDequeue(out command);
        if (!result) {
          throw new Exception("Someone else is dequeueing!");
        }
        if (command.isFlush) {
          shouldRedraw = true;
          continue;
        }
        var pt1 = GetPoint(command.strutIndex, 0);
        var pt2 = GetPoint(command.strutIndex, 1);
        int numLEDs = LEDDomeOutput.GetNumLEDs(command.strutIndex);
        double deltaX = (pt1.Item1 - pt2.Item1) / (numLEDs + 2.0);
        double deltaY = (pt1.Item2 - pt2.Item2) / (numLEDs + 2.0);
        int x = pt1.Item1 - (int)(deltaX * (command.ledIndex + 1));
        int y = pt1.Item2 - (int)(deltaY * (command.ledIndex + 1));
        uint color = (uint)SimulatorUtils.GetComputerColor(command.color)
          | (uint)0xFF000000;
        this.SetPixelColor(this.pixels, x, y, color);
        //this.SetPixelColor(this.pixels, x + 1, y, color);
        //this.SetPixelColor(this.pixels, x, y + 1, color);
        //this.SetPixelColor(this.pixels, x + 1, y + 1, color);
      }

      if (shouldRedraw && !this.keyMode) {
        this.bitmap.WritePixels(this.rect, this.pixels, this.rect.Width * 4, 0);
      }

      //Debug.WriteLine("DomeSimulator took " + stopwatch.ElapsedMilliseconds + "ms to update");
    }

    private void ShowKey(object sender, RoutedEventArgs e) {
      this.keyMode = !this.keyMode;
      foreach (Label strutLabel in this.strutLabels) {
        strutLabel.Visibility = this.keyMode
          ? Visibility.Visible
          : Visibility.Collapsed;
      }
      this.showKey.Content = this.keyMode
        ? "Hide Key"
        : "Show Key";
      this.directionLabel.Visibility = this.keyMode
        ? Visibility.Visible
        : Visibility.Collapsed;
      this.previewBox.Visibility = this.keyMode
        ? Visibility.Visible
        : Visibility.Collapsed;

      if (!this.keyMode) {
        this.bitmap.WritePixels(this.rect, this.pixels, this.rect.Width * 4, 0);
        return;
      }

      var strutPixels = new byte[rect.Width * rect.Height * 4];
      for (int x = 0; x < rect.Width; x++) {
        for (int y = 0; y < rect.Height; y++) {
          this.SetPixelColor(strutPixels, x, y, (uint)0xFF000000);
        }
      }

      uint color = (uint)SimulatorUtils.GetComputerColor(0xFFFFFF)
        | (uint)0xFF000000;
      for (int i = 0; i < LEDDomeOutput.GetNumStruts(); i++) {
        var pt1 = GetPoint(i, 0);
        var pt2 = GetPoint(i, 1);
        int numLEDs = LEDDomeOutput.GetNumLEDs(i);
        double deltaX = (pt1.Item1 - pt2.Item1) / (numLEDs + 2.0);
        double deltaY = (pt1.Item2 - pt2.Item2) / (numLEDs + 2.0);
        for (int j = 0; j < numLEDs * 3 / 4; j++) {
          int x = pt1.Item1 - (int)(deltaX * (j + 1));
          int y = pt1.Item2 - (int)(deltaY * (j + 1));
          this.SetPixelColor(strutPixels, x, y, color);
        }
      }

      this.bitmap.WritePixels(this.rect, strutPixels, this.rect.Width * 4, 0);
    }

    private void PreviewBoxLostFocus(object sender, RoutedEventArgs e) {
      if (String.IsNullOrEmpty(this.previewBox.Text)) {
        this.previewBox.Text = "Click some struts...";
        this.previewBox.Foreground = new SolidColorBrush(Colors.Gray);
        this.previewBox.FontStyle = FontStyles.Italic;
      }
    }

    private void PreviewBoxGotFocus(object sender, RoutedEventArgs e) {
      if (String.Equals(this.previewBox.Text, "Click some struts...")) {
        this.previewBox.Text = "";
        this.previewBox.Foreground = new SolidColorBrush(Colors.Black);
        this.previewBox.FontStyle = FontStyles.Normal;
      }
    }

    private void PreviewBoxTextChanged(object sender, TextChangedEventArgs e) {
      if (this.previewBox.IsFocused) {
        return;
      }
      if (String.IsNullOrEmpty(this.previewBox.Text)) {
        this.previewBox.Text = "Click some struts...";
        this.previewBox.Foreground = new SolidColorBrush(Colors.Gray);
        this.previewBox.FontStyle = FontStyles.Italic;
      } else if (!String.Equals(this.previewBox.Text, "Click some struts...")) {
        this.previewBox.Foreground = new SolidColorBrush(Colors.Black);
        this.previewBox.FontStyle = FontStyles.Normal;
      }
    }

    private void StrutLabelClicked(object sender, MouseButtonEventArgs e) {
      string[] listedStruts;
      if (
        String.IsNullOrEmpty(this.previewBox.Text) ||
        String.Equals(this.previewBox.Text, "Click some struts...")
      ) {
        listedStruts = new string[0];
      } else {
        listedStruts = this.previewBox.Text.Split(',');
      }
      string[] newListedStruts = new string[listedStruts.Length + 1];
      Array.Copy(listedStruts, newListedStruts, listedStruts.Length);
      newListedStruts[listedStruts.Length] =
        ((Label)e.Source).Content.ToString();
      this.previewBox.Text = String.Join(",", newListedStruts);
      this.previewBox.Focus();
      this.previewBox.Select(this.previewBox.Text.Length, 0);
    }

  }

}
