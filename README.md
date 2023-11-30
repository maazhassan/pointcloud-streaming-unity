# Point Cloud Player Unity Client

## Overview

A Unity project that creates a livestreaming client for PLY files. The files are grabbed from an HTTP server and are displayed using a ComputeBuffer shader. Many files and functions are adapted from [Pcx](https://github.com/keijiro/Pcx).

Defines one GameObject called `PointCloudContainer` which is responsible for everything.

Only works with little-endian binary PLY files.

## Setup & How-To

The PLY files (frames) should already be served using an HTTP server. Just change the `BASE_URL` variable inside `PointCloudContainer.cs` to point to the location of the PLY files. The expected naming convention for the frames is `frame_<number>.ply}`. Frames will continuously be grabbed from the server until a request fails.