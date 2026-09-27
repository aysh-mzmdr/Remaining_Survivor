using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds the level geometry (Grid/Ground tilemap) described by game1.png: a floor, ceiling,
// left/right walls, three platform tiers with jump gaps, and a walled alcove on the right
// housing the upper enemy ledge. Re-run from the menu after editing the layout below.
public static class LevelBuilder
{
    const string TilePath = "Assets/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/TX Tileset Ground_0.asset";

    // Grid origin in world space. Row 0's top edge lands on y = -4.39 to match the Knight's
    // existing spawn/ground height, so KnightMove's vertical tuning doesn't need to change.
    const float BaseX = -12f;
    const float BaseY = -5.39f;
    const int Cols = 24;
    const int Rows = 9;

    [MenuItem("Tools/Remaining Survivor/Build Level")]
    public static void Build()
    {
        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(TilePath);
        if (tile == null)
        {
            Debug.LogError("LevelBuilder: could not load tile at " + TilePath);
            return;
        }

        GameObject gridGO = GameObject.Find("Grid");
        if (gridGO == null)
        {
            gridGO = new GameObject("Grid", typeof(Grid));
        }
        gridGO.transform.position = new Vector3(BaseX, BaseY, 0f);

        Transform groundT = gridGO.transform.Find("Ground");
        GameObject groundGO = groundT != null
            ? groundT.gameObject
            : new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D), typeof(Rigidbody2D), typeof(CompositeCollider2D));
        if (groundT == null) groundGO.transform.SetParent(gridGO.transform, false);

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) groundGO.layer = groundLayer;

        groundGO.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        groundGO.GetComponent<TilemapCollider2D>().usedByComposite = true;
        groundGO.GetComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;

        Tilemap tilemap = groundGO.GetComponent<Tilemap>();
        tilemap.ClearAllTiles();

        void Fill(int c0, int c1, int row)
        {
            for (int c = c0; c <= c1; c++)
            {
                Vector3Int pos = new Vector3Int(c, row, 0);
                tilemap.SetTile(pos, tile);
                tilemap.SetColliderType(pos, Tile.ColliderType.Grid);
            }
        }

        // Floor and ceiling
        Fill(0, Cols - 1, 0);
        Fill(0, Cols - 1, Rows - 1);
        // Left and right walls
        for (int r = 0; r < Rows; r++)
        {
            Fill(0, 0, r);
            Fill(Cols - 1, Cols - 1, r);
        }
        // Platform tiers. Each tier sits 2 rows above the one below it: 1 empty row for jump
        // clearance/headroom, plus 1 row for the tier's own tile - both required so a tier's
        // surface isn't buried inside the solid mass of the tier directly above it (which would
        // leave no raycastable ground and no room to stand, i.e. an unwalkable "crushed" tier).
        // KnightMove.jumpHeight must clear the resulting 2-unit step (see its comment).
        Fill(2, 18, 2);   // Tier E - lower platform (4 enemies)
        Fill(3, 22, 4);   // Tier C - mid platform, extends into the right alcove floor
        Fill(1, 12, 6);   // Tier A - top/start platform (left side)
        Fill(19, 22, 6);  // Tier A - right alcove ledge

        EditorUtility.SetDirty(tilemap);
        EditorUtility.SetDirty(groundGO);
        Debug.Log("LevelBuilder: level built (" + Cols + "x" + Rows + " grid at " + gridGO.transform.position + ").");
    }
}
