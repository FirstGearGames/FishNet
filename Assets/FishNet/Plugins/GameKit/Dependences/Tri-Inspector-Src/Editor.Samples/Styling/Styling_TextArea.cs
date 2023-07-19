using UnityEngine;

public class Styling_TextArea : ScriptableObject
{
    public string simpleText;

    [TextArea(10, 15)]
    public string textArea;
}