﻿using Serenity;
using Serenity.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace Serenity.Demo.Northwind;

public partial class ShipperFormatterAttribute : CustomFormatterAttribute
{
    public const string Key = "Serenity.Demo.Northwind.ShipperFormatter";

    public ShipperFormatterAttribute()
        : base(Key)
    {
    }
}