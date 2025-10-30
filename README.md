# Fuzion Dock

Fuzion is a seamless game dock with automatic icon fetching and styling. It includes quite a few extra features such as deal fetching from reddit, omni search which searches in both steam and the dock, delayed and silent when available game launcher launching, and gamepad support. I no longer have the time to upkeep it.

## Features

Here are videos showing some of its features:

- [Feature Overview 1](https://www.youtube.com/watch?v=3J_RkttY18Y)
- [Feature Overview 2](https://www.youtube.com/watch?v=0SxQwtXNbsM)
- [Feature Overview 3](https://www.youtube.com/watch?v=EWxGA0x2igY)
- [Feature Overview 4](https://www.youtube.com/watch?v=I1nqhvt9KJk)
- [Feature Overview 5](https://www.youtube.com/watch?v=ta0jqpnwblw)
- [Feature Overview 6](https://www.youtube.com/watch?v=puYv7PPIAOk)

## Current Status

Fuzion has been published to the Windows Store as a UWP app and a Standalone app but is currently not showing the dock in the newer versions of Windows 10/11, probably because of changes in Desktop rendering.

**Windows Store Links:**
- [Fuzion Dock (UWP)](https://apps.microsoft.com/detail/9MTL580GPQ00?hl=en-us&gl=US&ocid=pdpshare)
- [Fuzion Dock (Standalone)](https://apps.microsoft.com/detail/XP8C9QP4X6CN53?hl=en-US&gl=US&ocid=pdpshare)

## Looking for a Maintainer

I'm looking for someone who wants to seriously take on the project and manage the public repository. If you're interested, please contact me using the Fuzion Discord: https://discord.gg/KQRrT6JBUv

## Configuration

This project requires some setup that you'll need to figure out by exploring the code. Off the top of my head, it needs:
- IGDB API key
- Steam API key
- A database for caching

Check the code for the specific environment variables and configuration required.

## License

See [LICENSE](LICENSE) file for details.
