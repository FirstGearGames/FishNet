using System;
using System.Collections.Generic;
using TriInspector;
using UnityEngine;

public class Misc_HideReferencePickerSample : ScriptableObject
{
    [Title("With Reference Picker")]
    [SerializeReference, PropertyOrder(1)]
    public MyReferenceType referenceField = new MyReferenceType();

    [ShowInInspector, PropertyOrder(2)]
    public MyReferenceType ReferenceProperty { get; set; } = new MyReferenceType();

    [Title("Without Reference Picker")]
    [SerializeReference, PropertyOrder(3), HideReferencePicker]
    public MyReferenceType genericField = new MyReferenceType();

    [ShowInInspector, PropertyOrder(4), HideReferencePicker]
    public MyReferenceType GenericProperty { get; set; } = new MyReferenceType();

    [Title("List With Reference Picker")]
    [SerializeReference, PropertyOrder(5)]
    public List<MyReferenceType> listReferenceField = new List<MyReferenceType>
    {
        new MyReferenceType()
    };

    [ShowInInspector, PropertyOrder(6)]
    public List<MyReferenceType> ListReferenceProperty { get; set; } = new List<MyReferenceType>
    {
        new MyReferenceType()
    };

    [Title("List Without Reference Picker")]
    [SerializeReference, PropertyOrder(7), HideReferencePicker]
    public List<MyReferenceType> listGenericField = new List<MyReferenceType>
    {
        new MyReferenceType()
    };

    [ShowInInspector, PropertyOrder(8), HideReferencePicker]
    public List<MyReferenceType> ListGenericProperty { get; set; } = new List<MyReferenceType>
    {
        new MyReferenceType()
    };

    [Serializable]
    public class MyReferenceType
    {
        public float a;
        public bool b;
    }
}