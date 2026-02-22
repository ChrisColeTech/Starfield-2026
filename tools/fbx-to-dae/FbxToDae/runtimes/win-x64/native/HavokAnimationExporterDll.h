#pragma once

#ifdef __cplusplus
extern "C" {
#endif

#define HAE_SUCCESS 0
#define HAE_ERROR_INVALID_ARGUMENT 1
#define HAE_ERROR_FILE_NOT_FOUND 2
#define HAE_ERROR_LOAD_FAILED 3
#define HAE_ERROR_NO_SKELETON 4
#define HAE_ERROR_NO_ANIMATION 5
#define HAE_ERROR_SAVE_FAILED 6
#define HAE_ERROR_INIT_FAILED 7

__declspec(dllexport) int hae_initialize(void);
__declspec(dllexport) void hae_shutdown(void);
__declspec(dllexport) const char* hae_get_last_error(void);

__declspec(dllexport) int hae_convert_skeleton(const char* fbx_path, const char* output_path);

__declspec(dllexport) int hae_convert_animation(
    const char* fbx_path,
    const char* skeleton_path,
    const char* output_path,
    float fps,
    int compress
);

__declspec(dllexport) int hae_convert_animation_ex(
    const char* fbx_path,
    const char* skeleton_path,
    const char* output_path,
    float fps,
    int compress,
    int platform
);

#define HAE_PLATFORM_WINDOWS 0
#define HAE_PLATFORM_XBOX360 1
#define HAE_PLATFORM_PS3 2
#define HAE_PLATFORM_WIIU 3

#ifdef __cplusplus
}
#endif
