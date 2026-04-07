using Verse;

#nullable disable
namespace GW40K_Necrons;

/// <summary>
/// Marker worker class for Necron summoning recipes at the Monolith.
/// Actual spawn logic is handled by Patch_FinishRecipeAndStartStoringProduct,
/// which reads RecipeExtension_SpawnMech from the RecipeDef.
/// </summary>
public class RecipeWorker_SpawnAndBindMech : RecipeWorker
{
}
