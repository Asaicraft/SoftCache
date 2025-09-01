using System;
using System.Collections.Generic;
using System.Text;

namespace SoftCache.Generator.SoftCacheMaker.WritePolicyMakers;
public sealed class SimpleWritePolicyMaker : WritePolicyMaker
{
    public static readonly SimpleWritePolicyMaker Instance = new();
}
