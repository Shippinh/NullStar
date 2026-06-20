using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class ContactDamageTable : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string tag;
        public int damage;
        public bool instantKill;
        // whatever else you need per contact type
    }
    public List<Entry> entries;

    public Entry GetEntry(string tag) => entries.Find(e => e.tag == tag);
}
