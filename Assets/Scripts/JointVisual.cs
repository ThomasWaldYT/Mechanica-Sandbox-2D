// JointVisual.cs
// Keeps a joint?sprite *one step* above BOTH parts it connects.

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class JointVisual : MonoBehaviour
{
    private SpriteRenderer mySR;
    private SpriteRenderer partA;
    private SpriteRenderer partB;

    public void Init(SpriteRenderer a, SpriteRenderer b)
    {
        partA = a;
        partB = b;
        if (!mySR) mySR = GetComponent<SpriteRenderer>();
        RefreshOrder();
    }

    private void Awake()
    {
        mySR = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        RefreshOrder();
    }

    private void RefreshOrder()
    {
        if (!partA || !partB) return;

        int target = Mathf.Max(partA.sortingOrder, partB.sortingOrder) + 1;

        if (mySR.sortingOrder != target)
        {
            mySR.sortingOrder = target;
            SortingOrderManager.EnsureAtLeast(target);
        }
    }
}
