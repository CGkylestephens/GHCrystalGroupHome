using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;

namespace CrystalGroupHome.SharedRCL.Helpers
{
    public static class ActiveDirectoryHelper
    {
        public static async Task<List<string>> GetGroupUsersAsync(string groupName)
        {
            if (!OperatingSystem.IsWindows()) return [];

            List<string> users = new List<string>();

            // Wrap the synchronous PrincipalContext operations in Task.Run
            await Task.Run(async () =>
            {
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain))
                {
                    // Find the top-level group by its name.
                    GroupPrincipal group = GroupPrincipal.FindByIdentity(context, groupName);
                    if (group != null)
                    {
                        // Use a HashSet to track visited groups to prevent circular recursion.
                        HashSet<string> visitedGroups = new HashSet<string>();
                        await GetGroupUsersRecursiveAsync(group, users, visitedGroups);
                    }
                }
            });

            return users;
        }

        private static async Task GetGroupUsersRecursiveAsync(GroupPrincipal group, List<string> users, HashSet<string> visitedGroups)
        {
            if (!OperatingSystem.IsWindows()) return;

            // Add the current group to visited groups to prevent infinite recursion
            visitedGroups.Add(group.Name);

            // Get all members of the group - this operation can be time-consuming
            var members = await Task.Run(() => group.GetMembers());

            foreach (var member in members)
            {
                if (member is UserPrincipal user)
                {
                    // Add the user's SAM account name to the list
                    users.Add(user.SamAccountName);
                }
                else if (member is GroupPrincipal nestedGroup)
                {
                    // If the member is a group and we haven't visited it yet
                    if (!visitedGroups.Contains(nestedGroup.Name))
                    {
                        await GetGroupUsersRecursiveAsync(nestedGroup, users, visitedGroups);
                    }
                }
            }
        }
    }
}