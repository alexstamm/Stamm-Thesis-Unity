using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnvironmentSO", menuName = "SOList/Environment", order = 1)]
public class EnvironmentSO : ScriptableObject {
    public List<GameObject> bushRefs;
    public List<GameObject> treeRefs;
	
}
