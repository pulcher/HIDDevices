﻿// Licensed under the Apache License, Version 2.0 (the "License").
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using HIDDevices.Controllers;
using Microsoft.Extensions.Logging;

namespace HIDDevices.Sample.Samples
{
    public class GameLoopSample : Sample
    {
        /// <inheritdoc />
        public override string Description =>
            "Demonstrates a classic synchronous game loop.";

        /// <inheritdoc />
        protected override void Execute()
        {
            // Create a singleton instance of the controllers object, that we should dispose
            // on closing the game, here we use a using block, but can obviously call controllers.Dispose()
            using var devices = new Devices(CreateLogger<Devices>());

            Logger.LogInformation("Press A Button to exit!");

            // Holds a reference to the current gamepad, which is set asynchronously as they are detected.
            Gamepad? gamepad = null;
            var batch = 0;

            // Controller to any gamepads as they are found
            using var subscription = devices.Controller<Gamepad>().Subscribe(g =>
            {
                // If we already have a connected gamepad ignore any more.
                if (gamepad?.IsConnected == true) return;

                // Assign this gamepad and connect to it.
                gamepad = g;
                g.Connect();
                batch = 0;
                Logger.LogInformation($"{gamepad.Name} found!  Following controls were mapped:");
                foreach (var (control, infos) in g.Mapping)
                {
                    Logger.LogInformation(
                        $"  {control.Name} => {string.Join(", ", infos.Select(info => info.PropertyName))}");
                }
            });

            var timestamp = 0L;
            try
            {
                // Our 'game loop'
                while (true)
                {
                    // Sleep to simulate a game loop.
                    Thread.Sleep(15);

                    // If we haven't got a gamepad, or the current one isn't connected, wait for a connected gamepad.
                    var currentGamepad = gamepad;
                    if (currentGamepad?.IsConnected != true) { continue; }

                    // Look for any changes since the last detected change.
                    var changes = currentGamepad.ChangesSince(timestamp);
                    if (changes.Count > 0)
                    {
                        Console.WriteLine("");
                        Console.WriteLine($"Batch {++batch}");
                        foreach (var value in changes)
                        {
                            // We should update our timestamp to the last change we see.
                            if (timestamp < value.Timestamp) timestamp = value.Timestamp;
                            var valueStr = value.Value switch
                            {
                                bool b => b ? "Pressed" : "Not Pressed",
                                double d => d.ToString("F3"),
                                null => "<null>",
                                _ => value.Value.ToString()
                            };
                            Console.WriteLine(
                                $"  {value.PropertyName}: {valueStr} ({value.Elapsed.TotalMilliseconds:F3}ms)");
                        }
                    }

                    // Or directly access controls
                    if (currentGamepad.AButton)
                    {
                        Console.WriteLine("A Button pressed, finishing.");
                        return;
                    }
                }
            }
            finally
            {
                // Ensure gamepad connection is disposed to stop listening to the gamepad
                if (gamepad != null)
                {
                    gamepad.Dispose();
                    Console.WriteLine($"{gamepad.Device.Name} disconnected!");
                }
            }
        }
    }
}
