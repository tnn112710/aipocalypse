# NOTICES AND INFORMATION

## Graphics Interchange Format

Text Studio includes an original managed GIF89a encoder. It does not bundle a
third-party GIF library, codec binary, or native LZW implementation.

The Graphics Interchange Format is the Copyright property of CompuServe
Incorporated. GIF is a Service Mark property of CompuServe Incorporated.

Format specification: https://www.w3.org/Graphics/GIF/spec-gif89a.txt

## Unity Components

Text Studio depends on TextMeshPro under the Unity Companion License. MP4 preview
export is performed by Unity Editor's media encoder on supported editor platforms;
Text Studio does not redistribute a separate MP4 encoder or FFmpeg binary. Users are
responsible for any codec, patent, container, or distribution terms that apply to
their use of the encoded MP4 files.

## Geometry Dependencies

Text Studio base owns the following components in its TextStudio.Geometry assembly. Paths below are relative to the installed Text Studio base package.

## Clipper2

- Project: https://github.com/AngusJohnson/Clipper2
- License: Boost Software License 1.0
- Source: `Runtime/Geometry/ThirdParty/Clipper2Lib/`
- License text: `Runtime/Geometry/ThirdParty/Clipper2Lib/LICENSE.txt`

The vendored `HashCode.cs` file also retains its .NET Foundation MIT and
xxHash32 BSD 2-Clause notices inline.

## LibTessDotNet (double-precision build)

- Project: https://github.com/speps/LibTessDotNet
- Version: 1.1.15
- License: SGI Free Software License B 2.0
- Binary: `Runtime/Geometry/ThirdParty/LibTessDotNet-v1.1.15/LibTessDotNet.Double.dll`
- License text: `Runtime/Geometry/ThirdParty/LibTessDotNet-v1.1.15/LICENSE.txt`

## Typography.OpenFont

- Project: https://github.com/LayoutFarm/Typography
- Revision: `5877180`
- Licenses: MIT and Apache License 2.0 contributions
- Binary: `Runtime/Geometry/ThirdParty/Typography.OpenFont-5877180/Typography.OpenFont.dll`
- License texts: `Runtime/Geometry/ThirdParty/Typography.OpenFont-5877180/LICENSE-MIT.txt` and
  `Runtime/Geometry/ThirdParty/Typography.OpenFont-5877180/LICENSE-Apache-2.0.txt`
