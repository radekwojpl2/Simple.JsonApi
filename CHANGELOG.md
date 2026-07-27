# [1.1.0](https://github.com/radekwojpl2/Simple.JsonApi/compare/v1.0.0...v1.1.0) (2026-07-27)


### Features

* add Simple.JsonApi.OpenApi ([8428c17](https://github.com/radekwojpl2/Simple.JsonApi/commit/8428c173451a9c7cefe5794528bbf011a3b2e3d8))
* add Simple.JsonApi.OpenApi ([ca6baeb](https://github.com/radekwojpl2/Simple.JsonApi/commit/ca6baeb414e80cb95714d94606426efd297133bb))

# [1.0.0](https://github.com/radekwojpl2/json-api-format-poc/compare/v0.1.1...v1.0.0) (2026-07-23)


* feat!: drop the PoC app and JsonApiKit, ship JsonApiLite alone ([ec13b26](https://github.com/radekwojpl2/json-api-format-poc/commit/ec13b269c2ae1411bf3bbfa2476f047590e9f2f3))
* feat!: hold the type parameters to marker interfaces ([7dd0026](https://github.com/radekwojpl2/json-api-format-poc/commit/7dd00263af4696464c132ba4b4e96a1f54cfbd06))
* feat!: type every meta position ([a3aa78f](https://github.com/radekwojpl2/json-api-format-poc/commit/a3aa78fe522794acc1e4c279fc2f0c2ac30da10f))


### Bug Fixes

* carry meta everywhere the spec allows it ([60d42f1](https://github.com/radekwojpl2/json-api-format-poc/commit/60d42f1af7fa1b74775080b64e06bd51f2d64419))


### Features

* add JsonApiLite, a minimal strongly typed JSON:API document library ([b613241](https://github.com/radekwojpl2/json-api-format-poc/commit/b613241da019498e68e82773d6ebbb465fc00392))
* target net8.0 alongside net10.0 ([fdbe7b9](https://github.com/radekwojpl2/json-api-format-poc/commit/fdbe7b90d382e77724b713743d0f3316e1a46185))


### BREAKING CHANGES

* relationships records must implement IRelationships and
meta records IMeta; attributes records implementing IResourceType need
no change.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
* Meta no longer has Total, PageCount or Additional —
declare the shape and use Meta<T> or the document's TMeta parameter.
Link.Meta and Error.Meta are Meta rather than JsonObject.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
* the Simple.JsonApi.OpenApi and Simple.JsonApi.Testing
packages are no longer built or published, and Simple.JsonApi now ships
JsonApiLite instead of JsonApiKit.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>

## [0.1.1](https://github.com/radekwojpl2/json-api-format-poc/compare/v0.1.0...v0.1.1) (2026-07-15)


### Bug Fixes

* override vulnerable transitive packages ([226711f](https://github.com/radekwojpl2/json-api-format-poc/commit/226711f3409ebd023a15690aed0633f3a51ada9e))
* override vulnerable transitive packages (SQLitePCLRaw, Microsoft.OpenApi) ([af6b36b](https://github.com/radekwojpl2/json-api-format-poc/commit/af6b36b78b696f8f943e20598968449d4cea75c2))
* publish packages as Simple.JsonApi (JsonApiKit id is taken on nuget.org) ([32ca1d2](https://github.com/radekwojpl2/json-api-format-poc/commit/32ca1d2da860ba376dd5553bdb8ae71b06a89ec9))
