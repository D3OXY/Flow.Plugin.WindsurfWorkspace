// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Flow.Plugin.VSCodeWorkspaces.SshConfigParser;
using Flow.Plugin.VSCodeWorkspaces.VSCodeHelper;

namespace Flow.Plugin.VSCodeWorkspaces.RemoteMachinesHelper
{
    public class VSCodeRemoteMachinesApi
    {
        public VSCodeRemoteMachinesApi()
        {
        }

        private List<VSCodeRemoteMachine> _machines = new List<VSCodeRemoteMachine>();

        public List<VSCodeRemoteMachine> Machines
        {
            get { return _machines; }
        }

        public void LoadMachines(VSCodeInstance vscodeInstance)
        {
            _machines.Clear();

            // Load SSH config machines
            var sshConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "config");
            if (File.Exists(sshConfigPath))
            {
                try
                {
                    var configContent = File.ReadAllText(sshConfigPath);
                    var configLines = configContent.Split('\n');
                    var currentHost = string.Empty;
                    var currentHostName = string.Empty;
                    var currentUser = string.Empty;

                    foreach (var line in configLines)
                    {
                        var trimmedLine = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                            continue;

                        if (trimmedLine.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(currentHost) && !string.IsNullOrEmpty(currentHostName))
                            {
                                var machine = new VSCodeRemoteMachine
                                {
                                    Name = currentHost,
                                    Type = "ssh-remote",
                                    Host = currentHostName,
                                    User = currentUser,
                                    VSCodeInstance = vscodeInstance
                                };
                                _machines.Add(machine);
                            }

                            currentHost = trimmedLine["Host ".Length..].Trim();
                            currentHostName = string.Empty;
                            currentUser = string.Empty;
                        }
                        else if (trimmedLine.StartsWith("HostName ", StringComparison.OrdinalIgnoreCase))
                        {
                            currentHostName = trimmedLine["HostName ".Length..].Trim();
                        }
                        else if (trimmedLine.StartsWith("User ", StringComparison.OrdinalIgnoreCase))
                        {
                            currentUser = trimmedLine["User ".Length..].Trim();
                        }
                    }

                    // Add the last machine if it exists
                    if (!string.IsNullOrEmpty(currentHost) && !string.IsNullOrEmpty(currentHostName))
                    {
                        var machine = new VSCodeRemoteMachine
                        {
                            Name = currentHost,
                            Type = "ssh-remote",
                            Host = currentHostName,
                            User = currentUser,
                            VSCodeInstance = vscodeInstance
                        };
                        _machines.Add(machine);
                    }
                }
                catch (Exception ex)
                {
                    var message = $"Failed to parse SSH config file: {sshConfigPath}";
                    Main._context.API.LogException("VSCodeRemoteMachinesApi", message, ex);
                }
            }

            // Load GitHub Codespaces
            var codespaceStoragePath = Path.Combine(vscodeInstance.AppData, "User", "globalStorage", "GitHub.codespaces");
            if (Directory.Exists(codespaceStoragePath))
            {
                try
                {
                    var files = Directory.GetFiles(codespaceStoragePath, "*.json");
                    foreach (var file in files)
                    {
                        var content = File.ReadAllText(file);
                        var codespace = JsonSerializer.Deserialize<GitHubCodespace>(content);
                        if (codespace != null)
                        {
                            var machine = new VSCodeRemoteMachine
                            {
                                Name = codespace.FriendlyName,
                                Type = "codespaces",
                                Host = codespace.Name,
                                VSCodeInstance = vscodeInstance
                            };
                            _machines.Add(machine);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var message = $"Failed to parse GitHub Codespaces from: {codespaceStoragePath}";
                    Main._context.API.LogException("VSCodeRemoteMachinesApi", message, ex);
                }
            }
        }
    }
}