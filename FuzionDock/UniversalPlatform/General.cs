using System;

namespace Fuzion.UniversalPlatform
{
    static class General
    {
        /// <summary>
        /// Store ID for the Fuzion Dock listing (apps.microsoft.com/detail/9MTL580GPQ00).
        /// </summary>
        public const string StoreId = "9MTL580GPQ00";

        /// <summary>
        /// Opens the Store listing. Uses the ms-windows-store: protocol so it lands in the Store
        /// app itself, falling back to the web listing if the Store isn't available.
        /// </summary>
        public static void OpenStorePage()
        {
            try
            {
                System.Diagnostics.Process.Start("ms-windows-store://pdp/?productid=" + StoreId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to open the Store app, falling back to the web listing: " + ex.Message);

                try
                {
                    System.Diagnostics.Process.Start("https://apps.microsoft.com/detail/" + StoreId);
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine("Failed to open the Store listing: " + fallbackEx.Message);
                }
            }
        }
    }
}
