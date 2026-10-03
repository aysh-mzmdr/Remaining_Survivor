using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Turns loose ground sprites dragged into the scene (scene-root GameObjects with a SpriteRenderer
// showing a "TX Tileset Ground" sprite) into tiles on Grid/Ground, the solid tilemap the player and
// enemies raycast against. Each sprite is snapped to the cell containing its position, then deleted.
// Additive: existing tiles are kept, so drop in more sprites (or paint on Grid/Ground with the Tile
// Palette) and re-run any time.
public static class GroundBaker
{
    const string TileFolder = "Assets/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground";
    const string GeneratedTileFolder = "Assets/Tiles/Ground";
    const string TextureName = "TX Tileset Ground";

    [MenuItem("Tools/Remaining Survivor/Bake Ground Sprites")]
    public static void Bake()
    {
        // Play-mode edits are thrown away when play stops, including any sprites placed during play.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("GroundBaker: exit Play mode first. Sprites placed during Play mode are lost when it stops, so place them in Edit mode.");
            return;
        }

        Tilemap tilemap = GetOrCreateGroundTilemap();
        Dictionary<Sprite, TileBase> tilesBySprite = LoadTilesBySprite();

        var claimed = new Dictionary<Vector3Int, string>();
        int baked = 0, skipped = 0;

        Undo.SetCurrentGroupName("Bake Ground Sprites");
        Undo.RegisterCompleteObjectUndo(tilemap, "Bake Ground Sprites");

        foreach (GameObject go in tilemap.gameObject.scene.GetRootGameObjects())
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null || sr.sprite.texture == null) continue;
            if (!sr.sprite.texture.name.StartsWith(TextureName)) continue;
            if (go.transform.childCount > 0 || go.GetComponents<Component>().Length > 2) continue;

            Vector3Int cell = tilemap.WorldToCell(go.transform.position);
            claimed.TryGetValue(cell, out string owner);
            if (owner != null || tilemap.HasTile(cell))
            {
                Debug.LogWarning("GroundBaker: '" + go.name + "' lands on cell " + cell + " already taken by " +
                                 (owner ?? "an existing tile") + "; left it in the scene.", go);
                skipped++;
                continue;
            }

            TileBase tile = GetTile(sr.sprite, tilesBySprite);
            tilemap.SetTile(cell, tile);
            tilemap.SetColliderType(cell, Tile.ColliderType.Grid);

            // Keep rotation/flip, snapped to a multiple of 90 degrees.
            float z = Mathf.Round(go.transform.eulerAngles.z / 90f) * 90f;
            Vector3 scale = new Vector3(sr.flipX ? -1f : 1f, sr.flipY ? -1f : 1f, 1f);
            if (z != 0f || scale != Vector3.one)
            {
                tilemap.SetTileFlags(cell, TileFlags.None);
                tilemap.SetTransformMatrix(cell, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, z), scale));
            }

            claimed[cell] = go.name;
            Undo.DestroyObjectImmediate(go);
            baked++;
        }

        tilemap.CompressBounds();
        EditorUtility.SetDirty(tilemap);
        EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);

        BoundsInt b = tilemap.cellBounds;
        Vector3 min = tilemap.CellToWorld(b.min), max = tilemap.CellToWorld(b.max);
        Debug.Log("GroundBaker: baked " + baked + " sprites (" + skipped + " skipped). Ground now spans world x " +
                  min.x + ".." + max.x + ", y " + min.y + ".." + max.y + ".");
    }

    static Tilemap GetOrCreateGroundTilemap()
    {
        GameObject gridGO = GameObject.Find("Grid");
        if (gridGO == null)
        {
            gridGO = new GameObject("Grid", typeof(Grid));
            Undo.RegisterCreatedObjectUndo(gridGO, "Create Grid");
        }

        Transform groundT = gridGO.transform.Find("Ground");
        GameObject groundGO = groundT != null
            ? groundT.gameObject
            : new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D), typeof(Rigidbody2D), typeof(CompositeCollider2D));
        if (groundT == null)
        {
            groundGO.transform.SetParent(gridGO.transform, false);
            Undo.RegisterCreatedObjectUndo(groundGO, "Create Ground");
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) groundGO.layer = groundLayer;

        groundGO.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        groundGO.GetComponent<TilemapCollider2D>().usedByComposite = true;
        groundGO.GetComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;

        return groundGO.GetComponent<Tilemap>();
    }

    static Dictionary<Sprite, TileBase> LoadTilesBySprite()
    {
        var map = new Dictionary<Sprite, TileBase>();
        foreach (string folder in new[] { TileFolder, GeneratedTileFolder })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { folder }))
            {
                Tile t = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null && t.sprite != null && !map.ContainsKey(t.sprite)) map[t.sprite] = t;
            }
        }
        return map;
    }

    static TileBase GetTile(Sprite sprite, Dictionary<Sprite, TileBase> tilesBySprite)
    {
        if (tilesBySprite.TryGetValue(sprite, out TileBase existing)) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Tiles")) AssetDatabase.CreateFolder("Assets", "Tiles");
        if (!AssetDatabase.IsValidFolder(GeneratedTileFolder)) AssetDatabase.CreateFolder("Assets/Tiles", "Ground");

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        AssetDatabase.CreateAsset(tile, GeneratedTileFolder + "/" + sprite.name + ".asset");
        tilesBySprite[sprite] = tile;
        return tile;
    }
}
