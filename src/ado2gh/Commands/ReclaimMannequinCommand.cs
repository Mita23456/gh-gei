﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octoshift;

namespace OctoshiftCLI.AdoToGithub.Commands
{
    public class ReclaimMannequinCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly GithubApiFactory _githubApiFactory;
        private ReclaimService _reclaimService;

        internal Func<string, bool> FileExists = (path) => File.Exists(path);
        internal Func<string, string[]> GetFileContent = (path) => File.ReadLines(path).ToArray();

        public ReclaimMannequinCommand(OctoLogger log, GithubApiFactory githubApiFactory, ReclaimService reclaimService = null) : base("reclaim-mannequin")
        {
            _log = log;
            _githubApiFactory = githubApiFactory;
            _reclaimService = reclaimService;

            Description = "Reclaims one or more mannequin user(s). An invite will be sent and the user(s) will have to accept for the remapping to occur."
              + "You can reclaim a single user by using --mannequin-user and --target-user or reclaim mannequins in bulk by using the --csv parameter"
              + Environment.NewLine
              + "The CSV file should contain a column with the user's login name (source) and reclaiming user login (target)."
              + Environment.NewLine
              + "The first line is considered the header and is ignored."
              + Environment.NewLine
              + "If both options are specified The CSV file takes precedence and other options will be ignored";

            var githubOrgOption = new Option<string>("--github-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-pat arg."
            };

            var csvOption = new Option<string>("--csv")
            {
                IsRequired = false,
                Description = "CSV file path with list of mannequins to be reclaimed."
            };

            var mannequinUsernameOption = new Option<string>("--mannequin-user")
            {
                IsRequired = false,
                Description = "The login of the mannequin to be remapped."
            };
            var mannequinIdOption = new Option<string>("--mannequin-id")
            {
                IsRequired = false,
                Description = "The Id of the mannequin, in case there are multiple mannequins with the same login you can specify the id to reclaim one of the mannequins."
            };

            var targetUsernameOption = new Option<string>("--target-user")
            {
                IsRequired = false,
                Description = "The login of the target user to be mapped."
            };

            var forceOption = new Option("--force")
            {
                IsRequired = false,
                Description = "Map the user even if it was previously mapped"
            };
            var githubPatOption = new Option<string>("--github-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubOrgOption);
            AddOption(csvOption);
            AddOption(mannequinUsernameOption);
            AddOption(mannequinIdOption);
            AddOption(targetUsernameOption);
            AddOption(forceOption);
            AddOption(githubPatOption);
            AddOption(verbose);

            Handler = CommandHandler.Create<string, string, string, string, string, bool, string, bool>(Invoke);
        }

        public async Task Invoke(
          string githubOrg,
          string csv,
          string mannequinUser,
          string mannequinId,
          string targetUser,
          bool force = false,
          string githubPat = null,
          bool verbose = false)
        {
            _log.Verbose = verbose;

            if (string.IsNullOrEmpty(csv) && (string.IsNullOrEmpty(mannequinUser) || string.IsNullOrEmpty(targetUser)))
            {
                throw new OctoshiftCliException("Either --csv or --mannequin-user and --target-user must be specified");
            }

            var githubApi = _githubApiFactory.Create(personalAccessToken: githubPat);
            if (_reclaimService == null)
            {
                _reclaimService = new ReclaimService(githubApi, _log);
            }

            if (!string.IsNullOrEmpty(csv))
            {
                _log.LogInformation("Reclaming Mannequins with CSV...");

                _log.LogInformation($"GITHUB ORG: {githubOrg}");
                _log.LogInformation($"FILE: {csv}");
                if (force)
                {
                    _log.LogInformation("MAPPING RECLAIMED");
                }

                if (!FileExists(csv))
                {
                    throw new OctoshiftCliException($"File {csv} does not exist.");
                }

                await _reclaimService.ReclaimMannequins(GetFileContent(csv), githubOrg, force);
            }
            else
            {
                _log.LogInformation("Reclaming Mannequin...");

                _log.LogInformation($"GITHUB ORG: {githubOrg}");
                _log.LogInformation($"MANNEQUIN: {mannequinUser}");
                if (mannequinId != null)
                {
                    _log.LogInformation($"MANNEQUIN ID: {mannequinId}");
                }
                _log.LogInformation($"RECLAIMING USER: {targetUser}");
                if (githubPat is not null)
                {
                    _log.LogInformation("GITHUB PAT: ***");
                }
                _log.LogInformation($"GITHUB ORG: {githubOrg}");

                await _reclaimService.ReclaimMannequin(mannequinUser, mannequinId, targetUser, githubOrg, force);
            }
        }
    }
}