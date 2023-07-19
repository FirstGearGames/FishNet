# Tri Inspector [![Github license](https://img.shields.io/github/license/codewriter-packages/Tri-Inspector.svg?style=flat-square)](#) [![Unity 2020.3](https://img.shields.io/badge/Unity-2020.3+-2296F3.svg?style=flat-square)](#) ![GitHub package.json version](https://img.shields.io/github/package-json/v/codewriter-packages/Tri-Inspector?style=flat-square) [![openupm](https://img.shields.io/npm/v/com.codewriter.triinspector?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.codewriter.triinspector/)

_Advanced inspector attributes for Unity_

![Tri-Inspector-Demo](https://user-images.githubusercontent.com/26966368/187032895-8c41295b-dd82-40ad-80c3-1efaad202732.png)

- [How to Install](#How-to-Install)
- [Roadmap](#Roadmap-)
- [Samples](#Samples)
- [Attributes](#Attributes)
    - [Misc](#Misc)
    - [Validation](#Validation)
    - [Decorators](#Decorators)
    - [Styling](#Styling)
    - [Collections](#Collections)
    - [Conditionals](#Conditionals)
    - [Buttons](#Buttons)
    - [Debug](#Debug)
    - [Groups](#Groups)
- [Integrations](#Integrations)
    - [Odin Inspector](#Odin-Inspector)
    - [Odin Validator](#Odin-Validator)
- [License](#License)

## How to Install

Library distributed as git package ([How to install package from git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html))
<br>Git URL: `https://github.com/codewriter-packages/Tri-Inspector.git`

After package installation **run the Installer** by double clicking on it.
![TriInspector-Installer](https://user-images.githubusercontent.com/26966368/172212210-3bbcf6ff-cdc3-4c7c-87c6-d27ab83e271c.png)

## Roadmap ![GitHub Repo stars](https://img.shields.io/github/stars/codewriter-packages/Tri-Inspector?style=social)
Each star ★ on the project page brings new features closer. 
You can suggest new features in the [Discussions](https://github.com/codewriter-packages/Tri-Inspector/discussions).

## Samples

TriInspector has built-in samples at `Tools/Tri Inspector/Samples` menu.
![Samples](https://user-images.githubusercontent.com/26966368/177045336-a3fcf438-3e70-45d0-b753-299e577b2010.png)

## Attributes

### Misc

#### ShowInInspector

Shows non-serialized property in the inspector.

![ShowInInspector](https://user-images.githubusercontent.com/26966368/168230693-a1a389a6-1a3b-4b94-b4b5-0764e88591f4.png)

```csharp
private float _field;

[ShowInInspector]
private bool _myToggle;

[ShowInInspector]
public float ReadOnlyProperty => _field;

[ShowInInspector]
public float EditableProperty
{
    get => _field;
    set => _field = value;
}
```

#### HideReferencePicker

Tri Inspector by default shows a polymorphic type picker for `[SerializeReference]` and `[ShowInInspector]`. It can be hidden with a `[HideReferencePicker]` attribute.

![HideReferencePicker](https://user-images.githubusercontent.com/26966368/182633485-a7876052-afd4-40f4-bc6b-be61a04997a4.png)

```csharp
[SerializeReference]
public MyReferenceClass clazz1 = new MyReferenceClass();

[SerializeReference, HideReferencePicker]
public MyReferenceClass clazz2 = new MyReferenceClass();

[ShowInInspector, HideReferencePicker]
public MyReferenceClass Clazz3 { get; set; } = new MyReferenceClass();

[Serializable]
public class MyReferenceClass
{
    public int inner;
}
```

#### PropertyOrder

Changes property order in the inspector.

![PropertyOrder](https://user-images.githubusercontent.com/26966368/168231223-c6628a8d-0d0a-47c1-8850-dc4e789fa14f.png)

```csharp
public float first;

[PropertyOrder(0)]
public float second;
```

#### ReadOnly

Makes property non-editable in the inspector.

![ReadOnly](https://user-images.githubusercontent.com/26966368/168231817-948ef153-eb98-42fb-88ad-3e8d17925b43.png)

```csharp
[ReadOnly]
public Vector3 vec;
```

#### OnValueChanged

Invokes callback on property modification.

```csharp
[OnValueChanged(nameof(OnMaterialChanged))]
public Material mat; 

private void OnMaterialChanged()
{
    Debug.Log("Material changed!");
}
```

### Validation

Tri Inspector has some builtin validators such as `missing reference` and `type mismatch` error. Additionally you can mark out your code with validation attributes or even write own validators.

![Builtin-Validators](https://user-images.githubusercontent.com/26966368/168232996-04de69a5-91c2-45d8-89b9-627b498db2ce.png)

#### Required

![Required](https://user-images.githubusercontent.com/26966368/168233232-596535b4-bab8-462e-b5d8-7a1c090e5143.png)

```csharp
[Required]
public Material mat;
```

#### ValidateInput

![ValidateInput](https://user-images.githubusercontent.com/26966368/168233592-b4dcd4d4-88ec-4213-a2e5-667719feb0b8.png)

```csharp
[ValidateInput(nameof(ValidateTexture))]
public Texture tex;

private TriValidationResult ValidateTexture()
{
    if (tex == null) return TriValidationResult.Error("Tex is null");
    if (!tex.isReadable) return TriValidationResult.Warning("Tex must be readable");
    return TriValidationResult.Valid;
}

```

#### InfoBox

![InfoBox](https://user-images.githubusercontent.com/26966368/169318171-d1a02212-48f1-41d1-b0aa-e2e1b25df262.png)

```csharp
[Title("InfoBox Message Types")]
[InfoBox("Default info box")]
public int a;

[InfoBox("None info box", TriMessageType.None)]
public int b;

[InfoBox("Warning info box", TriMessageType.Warning)]
public int c;

[InfoBox("Error info box", TriMessageType.Error)]
public int d;

[InfoBox("$" + nameof(DynamicInfo), visibleIf: nameof(VisibleInEditMode))]
public Vector3 vec;

private string DynamicInfo => "Dynamic info box: " + DateTime.Now.ToLongTimeString();

private bool VisibleInEditMode => !Application.isPlaying;
```

#### AssetsOnly

![AssetsOnly](https://user-images.githubusercontent.com/26966368/173064367-3cfb17f7-e050-4fcb-9b0c-f8710f1716e7.png)

```csharp
[AssetsOnly]
public GameObject obj;
```

#### SceneObjectsOnly

![SceneObjectsOnly](https://user-images.githubusercontent.com/26966368/178470605-618c9796-054f-40bb-9c09-2d9c6f342faf.png)

```csharp
[SceneObjectsOnly]
public GameObject obj;
```

### Decorators

#### Dropdown

![Dropdown](https://user-images.githubusercontent.com/26966368/177182904-8bb40579-3dc5-441b-8f6b-3ff3d274f71f.png)

```csharp
[Dropdown(nameof(intValues))]
public int intValue = 1;

[Dropdown(nameof(GetVectorValues))]
public Vector3 vectorValue;

private int[] intValues = {1, 2, 3, 4, 5};

private IEnumerable<TriDropdownItem<Vector3>> GetVectorValues()
{
    return new TriDropdownList<Vector3>
    {
        {"Zero", Vector3.zero},
        {"One/Forward", Vector3.forward},
        {"One/Backward", Vector3.back},
    };
}
```

#### Scene

![Scene](https://user-images.githubusercontent.com/26966368/179394466-a9397212-e3bc-40f1-b721-8f7c43aa3048.png)

```csharp
[Scene] public string scene;
```

### Styling

#### Title

![Title](https://user-images.githubusercontent.com/26966368/168528842-10ba070e-74ab-4377-8f33-7a55609494f4.png)

```csharp
[Title("My Title")]
public string val;

[Title("$" + nameof(_myTitleField))]
public Rect rect;

[Title("$" + nameof(MyTitleProperty))]
public Vector3 vec;

[Title("Button Title")]
[Button]
public void MyButton()
{
}

private string _myTitleField = "Serialized Title";

private string MyTitleProperty => DateTime.Now.ToLongTimeString();
```

#### HideLabel

![HideLabel](https://user-images.githubusercontent.com/26966368/168528934-353f2843-b6ea-4f4f-b56e-24e006eca6ae.png)

```csharp
[Title("Wide Vector")]
[HideLabel]
public Vector3 vector;

[Title("Wide String")]
[HideLabel]
public string str;
```

#### LabelText

![LabelText](https://user-images.githubusercontent.com/26966368/168529002-8fb17112-f74c-4535-b399-aefdb352f73a.png)

```csharp
[LabelText("Custom Label")]
public int val;

[LabelText("$" + nameof(DynamicLabel))]
public Vector3 vec;

public string DynamicLabel => DateTime.Now.ToShortTimeString();
```

#### LabelWidth

![LabelWidth](https://user-images.githubusercontent.com/26966368/168529051-c90bce09-92a7-4afd-b961-d19f03e826f3.png)

```csharp
public int defaultWidth;

[LabelWidth(40)]
public int thin;

[LabelWidth(300)]
public int customInspectorVeryLongPropertyName;
```

#### GUIColor

![GUIColor](https://user-images.githubusercontent.com/26966368/168529122-048cd964-358c-453b-ab3a-aa7137bab4f7.png)

```csharp
[GUIColor(0.8f, 1.0f, 0.6f)]
public Vector3 vec;

[GUIColor(0.6f, 0.9f, 1.0f)]
[Button]
public void BlueButton() { }

[GUIColor(1.0f, 0.6f, 0.6f)]
[Button]
public void RedButton() { }
```

#### Indent

![Indent](https://user-images.githubusercontent.com/26966368/168528565-2972221d-2cb3-49f1-8000-a425e4ff6cea.png)

```csharp
[Title("Custom Indent")]
[Indent]
public int a;

[Indent(2)]
public int b;

[Indent(3)]
public int c;

[Indent(4)]
public int d;
```

#### PropertySpace

![PropertySpace](https://user-images.githubusercontent.com/26966368/168529641-ee61c950-cb15-4a4e-986b-c9fa8c82dd4d.png)

```csharp
[Space, PropertyOrder(0)]
public Vector3 vecField;

[ShowInInspector, PropertyOrder(1)]
[PropertySpace(SpaceBefore = 10, SpaceAfter = 30)]
public Rect RectProperty { get; set; }

[PropertyOrder(2)]
public bool b;
```

#### PropertyTooltip

![PropertyTooltip](https://user-images.githubusercontent.com/26966368/168530124-95609470-a495-4eb3-9059-f6203ead995f.png)

```csharp
[PropertyTooltip("This is tooltip")]
public Rect rect;

[PropertyTooltip("$" + nameof(DynamicTooltip))]
public Vector3 vec;

public string DynamicTooltip => DateTime.Now.ToShortTimeString();
```

#### InlineEditor

![InlineEditor](https://user-images.githubusercontent.com/26966368/168234617-86a7f500-e635-46f8-90f2-5696e5ae7e63.png)

```csharp
[InlineEditor]
public Material mat;
```

#### InlineProperty

![InlineProperty](https://user-images.githubusercontent.com/26966368/168234909-1e6bec90-18ed-4d56-91ca-fe09118e1b72.png)

```csharp
public MinMax rangeFoldout;

[InlineProperty(LabelWidth = 40)]
public MinMax rangeInline;

[Serializable]
public class MinMax
{
    public int min;
    public int max;
}
```

### Collections

#### ListDrawerSettings

![ListDrawerSettings](https://user-images.githubusercontent.com/26966368/171126103-4fab58a3-db6c-487b-b616-f7aad528e2ab.png)

```csharp
[ListDrawerSettings(Draggable = true,
                    HideAddButton = false,
                    HideRemoveButton = false,
                    AlwaysExpanded = false)]
public List<Material> list;

[ListDrawerSettings(Draggable = false, AlwaysExpanded = true)]
public Vector3[] vectors;

```

#### TableList

![TableList](https://user-images.githubusercontent.com/26966368/171125460-679fe467-cf01-47e0-8674-b565ee3d4d7e.png)

```csharp
[TableList(Draggable = true,
           HideAddButton = false,
           HideRemoveButton = false,
           AlwaysExpanded = false)]
public List<TableItem> table;

[Serializable]
public class TableItem
{
    [Required]
    public Texture icon;
    public string description;

    [Group("Combined"), LabelWidth(16)]
    public string A, B, C;

    [Button, Group("Actions")]
    public void Test1() { }

    [Button, Group("Actions")]
    public void Test2() { }
}
```

### Conditionals

#### ShowIf

![ShowIf](https://user-images.githubusercontent.com/26966368/168531065-af5dad6a-8aea-4ca9-9730-da5feac0099a.png)

```csharp
public Material material;
public bool toggle;
public SomeEnum someEnum;

[ShowIf(nameof(material), null)]
public Vector3 showWhenMaterialIsNull;

[ShowIf(nameof(toggle))]
public Vector3 showWhenToggleIsTrue;

[ShowIf(nameof(toggle), false)]
public Vector3 showWhenToggleIsFalse;

[ShowIf(nameof(someEnum), SomeEnum.Two)]
public Vector3 showWhenSomeEnumIsTwo;

public enum SomeEnum { One, Two, Three }
```

#### HideIf

```csharp
public bool visible;

[HideIf(nameof(visible))]
public float val;
```

#### EnableIf

```csharp
public bool visible;

[EnableIf(nameof(visible))]
public float val;
```

#### DisableIf

```csharp
public bool visible;

[DisableIf(nameof(visible))]
public float val;
```

#### HideInPlayMode / ShowInPlayMode

```csharp
[HideInPlayMode] [ShowInPlayMode]
```

#### DisableInPlayMode / EnableInPlayMode

```csharp
[DisableInPlayMode] [EnableInPlayMode]
```

#### HideInEditMode / ShowInEditMode

```csharp
[HideInEditMode] [ShowInEditMode]
```

#### DisableInEditMode / EnableInEditMode

```csharp
[DisableInEditMode] [EnableInEditMode]
```

### Buttons

#### Button

![Button](https://user-images.githubusercontent.com/26966368/168235907-2b5ed6d4-d00b-4cd6-999c-432abd0a2230.png)

```csharp
[Button("Click me!")]
private void DoButton()
{
    Debug.Log("Button clicked!");
}
```

#### EnumToggleButtons

![EnumToggleButtons](https://user-images.githubusercontent.com/26966368/171126422-79d6ba55-7928-4178-9cc9-a807e3cb8b53.png)

```csharp
[EnumToggleButtons] public SomeEnum someEnum;
[EnumToggleButtons] public SomeFlags someFlags;

public enum SomeEnum { One, Two, Three }

[Flags] public enum SomeFlags
{
    A = 1 << 0,
    B = 1 << 1,
    C = 1 << 2,
    AB = A | B,
    BC = B | C,
}
```

### Debug

#### ShowDrawerChain

![ShowDrawerChain](https://user-images.githubusercontent.com/26966368/168531723-5f2b2d7a-a4c1-4727-8ab5-e7b82a52182e.png)

```csharp
[ShowDrawerChain]
[Indent]
[PropertySpace]
[Title("Custom Title")]
[GUIColor(1.0f, 0.8f, 0.8f)]
public Vector3 vec;
```

### Groups

Properties can be grouped in the inspector using the `Group` attribute.

```csharp
[Group("one")] public float a;
[Group("one")] public float b;

[Group("two")] public float c;
[Group("two")] public float d;

public float e;
```

If you have a lot of properties and group attributes take up too much space, then you can combine multiple properties at once using the `GroupNext` attribute.

```csharp
[GroupNext("one")]
public float a;
public float b;

[GroupNext("two")]
public float c;
public float d;

[UnGroupNext]
public float e;
```

#### Box Group

![BoxGroup](https://user-images.githubusercontent.com/26966368/177552426-8124b445-e235-43a2-9143-dd5d954dd9f8.png)

```csharp
[DeclareBoxGroup("box", Title = "My Box")]
public class BoxGroupSample : ScriptableObject
{
    [Group("box")] public int a;
    [Group("box")] public bool b;
}
```

#### Foldout Group

![FoldoutGroup](https://user-images.githubusercontent.com/26966368/201517886-4138ee55-33c2-4a1a-93bc-a3cda7745a4c.png)

```csharp
[DeclareFoldoutGroup("foldout", Title = "$" + nameof(DynamicTitle))]
public class FoldoutGroupSample : ScriptableObject
{
    [Group("foldout")] public int a;
    [Group("foldout")] public bool b;
    
    public string DynamicTitle => "My Foldout";
}
```

#### Tab Group

![TabGroup](https://user-images.githubusercontent.com/26966368/177552003-528a4e52-e340-460b-93e6-f56c07ac063b.png)

```csharp
[DeclareTabGroup("tabs")]
public class TabGroupSample : ScriptableObject
{
    [Group("tabs"), Tab("One")] public int a;
    [Group("tabs"), Tab("Two")] public float b;
    [Group("tabs"), Tab("Three")] public bool c;
}
```

#### Horizontal Group

![HorizontalGroup](https://user-images.githubusercontent.com/26966368/177551227-9df32c44-9482-4580-8144-5745af806f24.png)

```csharp
[DeclareHorizontalGroup("vars")]
public class HorizontalGroupSample : ScriptableObject
{
    [Group("vars")] public int a;
    [Group("vars")] public int b;
    [Group("vars")] public int c;
}
```

#### Vertical Group

![VerticalGroup](https://user-images.githubusercontent.com/26966368/177550644-9d0dc2b7-ed18-4d8f-997d-c4fff2c6d6cb.png)

```csharp

[DeclareHorizontalGroup("horizontal")]
[DeclareVerticalGroup("horizontal/vars")]
[DeclareVerticalGroup("horizontal/buttons")]
public class VerticalGroupSample : ScriptableObject
{
    [Group("horizontal/vars")] public float a;
    [Group("horizontal/vars")] public float b;

    [Button, Group("horizontal/buttons")]
    public void ButtonA() { }

    [Button, Group("horizontal/buttons")]
    public void ButtonB() { }
}
```

## Integrations

### Odin Inspector

Tri Inspector is able to work in compatibility mode with Odin Inspector. 
In this mode, the primary interface will be drawn by the Odin Inspector. However, 
parts of the interface can be rendered by the Tri Inspector.

In order for the interface to be rendered by Tri instead of Odin, 
it is necessary to mark classes with `[DrawWithTriInspector]` attribute.

Alternatively, you can mark the entire assembly with an attribute `[assembly:DrawWithTriInspector]`
to draw all types in the assembly using the Tri Inspector.

### Odin Validator

Tri Inspector is integrated with the Odin Validator
so all validation results from Tri attributes will be shown 
in the Odin Validator window.

![Odin-Validator-Integration](https://user-images.githubusercontent.com/26966368/169645537-d8f0b50f-46af-4804-95e8-337ff3b5ae83.png)

## License

Tri-Inspector is [MIT licensed](./LICENSE.md).
