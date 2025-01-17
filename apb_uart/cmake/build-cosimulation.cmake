cmake_minimum_required(VERSION 3.8)

if(POLICY CMP0074)
  cmake_policy(SET CMP0074 NEW)
endif()

if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release CACHE STRING "Type of build" FORCE)
  message(STATUS "Setting build type to ${CMAKE_BUILD_TYPE} as none was specified")
endif()

if(NOT USER_RENODE_DIR AND DEFINED ENV{RENODE_ROOT})
  message(STATUS "Using RENODE_ROOT from environment as USER_RENODE_DIR")
  set(USER_RENODE_DIR $ENV{RENODE_ROOT} CACHE PATH "Absolute (!) path to Renode root directory or any other that contains VerilatorIntegrationLibrary.")
else()
  set(USER_RENODE_DIR CACHE PATH "Path to Renode root directory or any other that contains VerilatorIntegrationLibrary, relative to build directory.")
  get_filename_component(USER_RENODE_DIR "${USER_RENODE_DIR}" ABSOLUTE BASE_DIR ${CMAKE_CURRENT_BINARY_DIR})
endif()

# Default arguments for compilation and linking
list(APPEND PROJECT_COMP_ARGS -Wall)

if(${CMAKE_SYSTEM_NAME} STREQUAL "Linux")
  if(${CMAKE_CXX_COMPILER_ID} STREQUAL "GNU")
    list(APPEND PROJECT_LINK_ARGS -static-libstdc++ -static-libgcc)
  endif()
endif()

if(NOT VIL_DIR)
  if(NOT USER_RENODE_DIR OR NOT IS_ABSOLUTE "${USER_RENODE_DIR}")
    message(FATAL_ERROR "Please set the CMake's USER_RENODE_DIR variable to an absolute (!) path to Renode root directory or any other that contains VerilatorIntegrationLibrary.\nPass the '-DUSER_RENODE_DIR=<ABSOLUTE_PATH>' switch if you configure with the 'cmake' command. Optionally, consider using 'ccmake' or 'cmake-gui' which make it easier.")
  endif()
  
  message(STATUS "Looking for Renode VerilatorIntegrationLibrary inside ${USER_RENODE_DIR}...")
  set(VIL_FILE verilator-integration-library.cmake)
  # Look for the ${VIL_FILE} in the whole ${USER_RENODE_DIR} tree
  #   (don't use `/*/` as then an additional directory is required between the two)
  file(GLOB_RECURSE VIL_FOUND ${USER_RENODE_DIR}*/${VIL_FILE})
  
  list(LENGTH VIL_FOUND VIL_FOUND_N)
  if(${VIL_FOUND_N} EQUAL 1)
    string(REPLACE "/${VIL_FILE}" "" VIL_DIR ${VIL_FOUND})
  elseif(${VIL_FOUND_N} GREATER 1)
    string(REGEX REPLACE "/${VIL_FILE}" " " ALL_FOUND ${VIL_FOUND})
    message(FATAL_ERROR "Found more than one directory with VerilatorIntegrationLibrary inside USER_RENODE_DIR. Please choose one of them: ${ALL_FOUND}")
  endif()
  
  if(NOT VIL_DIR OR NOT EXISTS "${VIL_DIR}/${VIL_FILE}")
    message(FATAL_ERROR "Couldn't find valid VerilatorIntegrationLibrary inside USER_RENODE_DIR!")
  endif()
  
  include(${VIL_DIR}/${VIL_FILE})  # sets VIL_VERSION variable
  message(STATUS "Renode VerilatorIntegrationLibrary (version ${VIL_VERSION}) found in ${VIL_DIR}.")
  
  # Save VIL_DIR in cache
  set(VIL_DIR ${VIL_DIR} CACHE INTERNAL "")
endif()

# Prepare list of Renode DPI Integration files
set(RENODE_HDL_LIBRARY ${VIL_DIR}/hdl)
file(GLOB RENODE_HDL_SOURCES ${RENODE_HDL_LIBRARY}/imports/*.sv)
list(APPEND RENODE_HDL_SOURCES ${RENODE_HDL_LIBRARY}/renode.sv)
file(GLOB_RECURSE RENODE_HDL_MODULES_SOURCES ${RENODE_HDL_LIBRARY}/modules/*.sv)
list(APPEND RENODE_HDL_SOURCES ${RENODE_HDL_MODULES_SOURCES})

file(GLOB_RECURSE RENODE_SOURCES ${VIL_DIR}/libs/socket-cpp/*.cpp)
list(APPEND RENODE_SOURCES ${VIL_DIR}/src/communication/socket_channel.cpp)
list(APPEND RENODE_SOURCES ${VIL_DIR}/src/renode_dpi.cpp)

if(NOT SIM_TOP OR NOT SIM_TOP_FILE)
  message(FATAL_ERROR "'SIM_TOP' and 'SIM_TOP_FILE' variable have to be set!")
endif()
set(ALL_SIM_FILES ${SIM_TOP_FILE})
list(APPEND ALL_SIM_FILES ${SIM_FILES})
list(APPEND ALL_SIM_FILES ${RENODE_HDL_SOURCES})
foreach(SIM_FILE ${ALL_SIM_FILES})
  get_filename_component(SIM_FILE ${SIM_FILE} ABSOLUTE BASE_DIR)
  list(APPEND FINAL_SIM_FILES ${SIM_FILE})
endforeach()

###
### Prepare Verilator target
###
set(USER_VERILATOR_ARGS ${VERILATOR_ARGS} CACHE STRING "Extra arguments/switches for Verilating")
separate_arguments(USER_VERILATOR_ARGS)
set(FINAL_VERILATOR_ARGS ${USER_VERILATOR_ARGS})
list(APPEND FINAL_VERILATOR_ARGS "-I${RENODE_HDL_LIBRARY}")
set(FINAL_LINK_ARGS ${PROJECT_LINK_ARGS})
set(FINAL_COMP_ARGS ${PROJECT_COMP_ARGS})

# Find Verilator
if(IS_DIRECTORY "${USER_VERILATOR_DIR}")
  # Verilator CMake logic prioritizes VERILATOR_ROOT environment variable
  message(STATUS "Using USER_VERILATOR_DIR instead of VERILATOR_ROOT environmental variable")
  get_filename_component(USER_VERILATOR_DIR ${USER_VERILATOR_DIR} ABSOLUTE BASE_DIR ${CMAKE_CURRENT_BINARY_DIR})
  set(ENV{VERILATOR_ROOT} ${USER_VERILATOR_DIR})
endif()
find_package(verilator HINTS ${USER_VERILATOR_DIR} $ENV{VERILATOR_ROOT})
if(NOT verilator_FOUND)
  set(USER_VERILATOR_DIR CACHE PATH "Path to the Verilator's root directory, relative to build directory.")
  message(NOTICE "There's no Verilator installed. This target will be ignored.")
else()
  if(NOT VERILATOR_CSOURCES)
    message(FATAL_ERROR "'VERILATOR_CSOURCES' it's required to set this variable for Verilator target!")
  endif()
  add_executable(verilated ${VERILATOR_CSOURCES} ${RENODE_SOURCES})
  target_include_directories(verilated PRIVATE ${VIL_DIR})
  target_compile_options(verilated PRIVATE ${FINAL_COMP_ARGS})
  target_link_libraries(verilated PRIVATE ${FINAL_LINK_ARGS})
  verilate(verilated SOURCES ${FINAL_SIM_FILES} TOP_MODULE ${SIM_TOP} PREFIX "V${SIM_TOP}" VERILATOR_ARGS ${FINAL_VERILATOR_ARGS})
endif()
