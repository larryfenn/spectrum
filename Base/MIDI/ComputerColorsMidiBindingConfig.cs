﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spectrum.Base {

  using BindingKey = Tuple<MidiCommandType, int>;

  public class ComputerColorsMidiBindingConfig : MidiBindingConfig {

    public MidiCommandType rangeType { get; set; }
    public int rangeStart { get; set; }
    public int rangeEnd { get; set; }

    public ComputerColorsMidiBindingConfig() {
      this.BindingType = 1;
    }

    public override object Clone() {
      return new ComputerColorsMidiBindingConfig() {
        BindingName = this.BindingName,
        rangeType = this.rangeType,
        rangeStart = this.rangeStart,
        rangeEnd = this.rangeEnd,
      };
    }

    public override Binding[] GetBindings(Configuration config) {
      Binding binding = new Binding();
      binding.key = new BindingKey(this.rangeType, -1);
      binding.callback = (index, val) => {
        if (index < this.rangeStart || index > this.rangeEnd) {
          return;
        }
        var colorIndex = index - this.rangeStart;
        config.colorPalette.computerEnabledColors[colorIndex] = val > 0.0;
      };
      return new Binding[] { binding };
    }

  }

}