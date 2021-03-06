﻿using Id3;
using Id3.Frames;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;

namespace Mp3CoverDroper.Implementation {

    public class Program {

        private static readonly string[] supportedImageExtensions = { ".jpg", ".jpeg", ".png" };

        static void Main(string[] args) {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length <= 1) {
                ShowHelp();
                return;
            }

            // get the arguments
            var mp3Path = args[0];
            var imagePaths = args.Skip(1).ToArray();
            if (Path.GetExtension(mp3Path).ToLower() != ".mp3") {
                ShowError("The given mp3 file has a non mp3 extension.");
                return;
            }
            if (!imagePaths.All(path => supportedImageExtensions.Contains(Path.GetExtension(path).ToLower()))) {
                ShowError("There are some images which has a non supported extension.");
                return;
            }
            if (!File.Exists(mp3Path) || !imagePaths.All(path => File.Exists(path))) {
                ShowError("Some files given to Mp3CoverDroper is not found, please check first.");
                return;
            }

            // get the mp3 file
            Mp3 mp3;
            try {
                mp3 = new Mp3(mp3Path, Mp3Permissions.ReadWrite);
            } catch (UnauthorizedAccessException ex) {
                var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator)) {
                    // not in admin, try to get admin authority
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = Application.ExecutablePath,
                        Arguments = string.Join(" ", args),
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    return;
                }
                ShowError($"You have no permission to write this mp3 file. Details:\n{ex}");
                return;
            } catch (Exception ex) {
                ShowError($"Failed to read mp3 file. Details:\n{ex}");
                return;
            }

            // process the mp3 file
            try {
                MainProcess(mp3, mp3Path, imagePaths);
            } catch (Exception ex) {
                ShowError($"Failed to execute the option. Details:\n{ex}");
            } finally {
                mp3.Dispose();
            }
        }

        private static void ShowHelp() {
            MessageBoxEx.Show($"Usage: {AppDomain.CurrentDomain.FriendlyName} [mp3 path] [image paths...]", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ShowError(string message) {
            MessageBoxEx.Show(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void MainProcess(Mp3 mp3, string mp3Path, string[] images) {
            var tag = mp3.GetTag(Id3TagFamily.Version2X);
            if (tag == null) {
                tag = new Id3Tag();
                if (!mp3.WriteTag(tag, Id3Version.V23, WriteConflictAction.Replace)) {
                    ShowError($"Failed to create tag to mp3 file.");
                }
            }

            // ask option
            bool addCover;
            bool removeFirst;
            var originCoverCount = tag.Pictures.Count;
            if (originCoverCount == 0) {
                var ok = MessageBoxEx.Show($"There is no cover in the given mp3 file, would you want to add the given {images.Length} cover(s) to this mp3 file?",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, new string[] { "&Add", "&Cancel" });
                addCover = ok == DialogResult.OK;
                removeFirst = false;
            } else {
                var ok = MessageBoxEx.Show($"The mp3 file has {originCoverCount} cover(s), would you want to remove it first, and add the given {images.Length} cover(s) to this mp3 file?",
                      MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation, new string[] { "&Replace", "&Append", "&Cancel" });
                addCover = ok != DialogResult.Cancel;
                removeFirst = ok == DialogResult.Yes;
            }

            // handle tag
            if (!addCover) {
                return;
            }
            if (removeFirst) {
                tag.Pictures.Clear();
            }
            foreach (var image in images) {
                var extension = Path.GetExtension(image).ToLower();
                var mime = "image/";
                if (extension == ".png") {
                    mime = "image/png";
                } else if (extension == ".jpg" || extension == ".jpeg") {
                    mime = "image/jpeg";
                }
                var newCover = new PictureFrame() {
                    PictureType = PictureType.FrontCover,
                    MimeType = mime
                };
                try {
                    newCover.LoadImage(image);
                } catch (Exception ex) {
                    ShowError($"Failed to load image: \"{image}\". Details:\n{ex}");
                    return;
                }
                tag.Pictures.Add(newCover);
            }

            // write tag
            mp3.DeleteTag(Id3TagFamily.Version2X);
            if (!mp3.WriteTag(tag, Id3Version.V23, WriteConflictAction.Replace)) {
                ShowError($"Failed to write cover(s) to mp3 file.");
                return;
            }

            string msg;
            if (removeFirst) {
                msg = $"Success to remove {originCoverCount} cover(s) and add {images.Length} cover(s) to mp3 file \"{mp3Path}\".";
            } else if (originCoverCount != 0) {
                msg = $"Success to add {images.Length} cover(s), now there are {images.Length + originCoverCount} covers in the mp3 file \"{mp3Path}\".";
            } else {
                msg = $"Success to add {images.Length} cover(s) to mp3 file \"{mp3Path}\".";
            }
            MessageBoxEx.Show(msg, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
