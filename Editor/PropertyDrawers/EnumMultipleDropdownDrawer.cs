﻿using System;
using Elarion.Attributes;
using Elarion.Editor.Extensions;
using UnityEditor;
using UnityEngine;

namespace Elarion.Editor.PropertyDrawers {

    [CustomPropertyDrawer(typeof(EnumMultipleDropdownAttribute))]
    public class EnumMultipleDropdownDrawer : PropertyDrawer {

        private EnumMultipleDropdownAttribute MultipleDropdownAttribute { get { return ((EnumMultipleDropdownAttribute)attribute); } }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var targetEnum = property.GetBaseProperty<Enum>();

            string propName = MultipleDropdownAttribute.name;
            if(string.IsNullOrEmpty(propName))
                propName = property.name;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            
            var newEnum = EditorGUI.EnumMaskField(position, label, targetEnum);
            if(EditorGUI.EndChangeCheck()) {
                property.intValue = (int)Convert.ChangeType(newEnum, targetEnum.GetType());
            }
            EditorGUI.EndProperty();
        }
    }
}
