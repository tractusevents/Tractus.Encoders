cmake_minimum_required(VERSION 3.21)

project(Tractus.AmfShim LANGUAGES CXX)

set(AMF_SDK_ROOT "" CACHE PATH "Path to the AMD AMF SDK root or include/header root")

if(NOT AMF_SDK_ROOT AND DEFINED ENV{AMF_SDK_ROOT})
    set(AMF_SDK_ROOT "$ENV{AMF_SDK_ROOT}")
endif()

if(NOT AMF_SDK_ROOT)
    message(FATAL_ERROR "Set AMF_SDK_ROOT to the AMD AMF SDK root or include/header root.")
endif()

set(AMF_INCLUDE_DIR "")
if(EXISTS "${AMF_SDK_ROOT}/core/Factory.h" AND EXISTS "${AMF_SDK_ROOT}/components/VideoEncoderVCE.h")
    set(AMF_INCLUDE_DIR "${AMF_SDK_ROOT}")
elseif(EXISTS "${AMF_SDK_ROOT}/amf/public/include/core/Factory.h" AND EXISTS "${AMF_SDK_ROOT}/amf/public/include/components/VideoEncoderVCE.h")
    set(AMF_INCLUDE_DIR "${AMF_SDK_ROOT}/amf/public/include")
elseif(EXISTS "${AMF_SDK_ROOT}/include/core/Factory.h" AND EXISTS "${AMF_SDK_ROOT}/include/components/VideoEncoderVCE.h")
    set(AMF_INCLUDE_DIR "${AMF_SDK_ROOT}/include")
else()
    message(FATAL_ERROR "Could not find AMF headers. Expected core/Factory.h and components/VideoEncoderVCE.h under AMF_SDK_ROOT.")
endif()

add_library(Tractus.AmfShim SHARED
    TractusAmfShim.cpp
    TractusAmfShim.h
)

target_compile_features(Tractus.AmfShim PRIVATE cxx_std_17)
target_include_directories(Tractus.AmfShim PRIVATE "${AMF_INCLUDE_DIR}")

if(MSVC)
    target_compile_options(Tractus.AmfShim PRIVATE /W4 /permissive-)
else()
    target_compile_options(Tractus.AmfShim PRIVATE -Wall -Wextra -Wpedantic)
endif()

if(UNIX AND NOT APPLE)
    target_link_libraries(Tractus.AmfShim PRIVATE dl pthread)
endif()

if(WIN32)
    set(TRACTUS_AMF_PLATFORM_DIR "win-x64")
elseif(UNIX AND NOT APPLE)
    set(TRACTUS_AMF_PLATFORM_DIR "linux-x64")
else()
    set(TRACTUS_AMF_PLATFORM_DIR "unsupported")
endif()

set(TRACTUS_AMF_ARTIFACT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/artifacts/${TRACTUS_AMF_PLATFORM_DIR}")

set_target_properties(Tractus.AmfShim PROPERTIES
    OUTPUT_NAME "Tractus.AmfShim"
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN YES
    RUNTIME_OUTPUT_DIRECTORY "${TRACTUS_AMF_ARTIFACT_DIR}"
    LIBRARY_OUTPUT_DIRECTORY "${TRACTUS_AMF_ARTIFACT_DIR}"
    ARCHIVE_OUTPUT_DIRECTORY "${TRACTUS_AMF_ARTIFACT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${TRACTUS_AMF_ARTIFACT_DIR}"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${TRACTUS_AMF_ARTIFACT_DIR}"
    ARCHIVE_OUTPUT_DIRECTORY_RELEASE "${TRACTUS_AMF_ARTIFACT_DIR}"
)
