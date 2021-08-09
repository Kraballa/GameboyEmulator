using System;
using System.Collections.Generic;
using System.Text;

namespace GBTesting
{
    //denotes a method to be run before every test
    [AttributeUsage(AttributeTargets.Method)]
    public class SetupAttribute : Attribute
    {
    }

    //denotes a method to be run after every test
    [AttributeUsage(AttributeTargets.Method)]
    public class CleanupAttribute : Attribute
    {
    }

    //denotes a test method
    [AttributeUsage(AttributeTargets.Method)]
    public class TestAttribute : Attribute
    {
    }
}
