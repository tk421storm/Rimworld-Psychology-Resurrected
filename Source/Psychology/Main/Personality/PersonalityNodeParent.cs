using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Psychology;

// XML entry in PersonalityNodeDef.parents: "this node correlates with <node> by <modifier>" (roughly a correlation
// coefficient in [-1, 1]; the matrix is made consistent afterwards, see PersonalityNodeMatrix).
public class PersonalityNodeParent
{
    //The parent node that correlates with this node.
    public PersonalityNodeDef node;
    // How the parent node correlates with this node.
    public float modifier;
}
