using System.Runtime.CompilerServices;
using UnityEngine;


namespace GameKit.Utilities.Types.CanvasContainers
{

    public class FloatingOptions : CanvasGroupFader
    {
        #region Protected.
        /// <summary>
        /// Current buttons.
        /// </summary>
        protected ButtonData[] Buttons;
        #endregion

        /// <summary>
        /// Adds buttons.
        /// </summary>
        /// <param name="clearExisting">True to clear existing buttons first.</param>
        /// <param name="buttonDatas">Buttons to add.</param>
        protected virtual void AddButtons(bool clearExisting, params ButtonData[] buttonDatas)
        {
            if (clearExisting)
                RemoveButtons();
            Buttons = buttonDatas;
        }

        /// <summary>
        /// Removes all buttons.
        /// </summary>
        protected virtual void RemoveButtons()
        {
            if (Buttons == null)
                return;

            foreach (ButtonData item in Buttons)
                GameKit.Utilities.ResettableObjectCaches<ButtonData>.Store(item);
        }


    }


}