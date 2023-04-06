using FishNet.Object;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttributeTest 
{

    private Item _item;
    [SetUp]
    public void SetUp()
    {
        GameObject go = new GameObject();
        _item = go.AddComponent<Item>();
    }

    [Test]
    public void CanUpdateWeight()
    {
        _item.UpdateWeight(0.5f);
        Assert.AreEqual(_item.Weight, 0.5f);
    }
}
