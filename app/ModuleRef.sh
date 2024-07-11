#!/bin/bash

modules=(
ApplicationCore
Core
CoreUObject
Engine
EngineSettings
InputCore
Json
Launch
MaterialShaderQualitySettings
PakFile
Projects
RenderCore
Renderer
VulkanRHI)

baseDir=/f/vulkanengine/Engine/Source/Runtime/

if [ -f result.txt ]; then
	rm result.txt -rf
fi

for currentModule in ${modules[@]}
do
	for userModule in ${modules[@]}
	do
		if [ $currentModule == $userModule ] || [ $currentModule == "Core" ]; then
			continue
		elif [ $currentModule == "Core" ] && [ $userModule == "CoreUObject" ]; then
			continue
		else
			result=`ModuleRef -m ${baseDir}${currentModule} -r ${baseDir}${userModule} -s "WITH_EDITOR,0,LOGTRACE_ENABLED,WITH_EDITOR_ONLY_DATA,UE_TRACE_ENABLED,!UE_BUILD_SHIPPING,VULKAN_HAS_DEBUGGING_ENABLED,RDG_ENABLE_DEBUG,WANTS_DRAW_MESH_EVENTS,!USE_BOOT_PROFILING,!(UE_BUILD_SHIPPING,!UE_BUILD_SHIPPING,STATICMESH_ENABLE_DEBUG_RENDERING,WANTS_DRAW_MESH_EVENTS,WITH_SERVER_CODE,INCLUDE_CHAOS,ENABLE_RHI_VALIDATION,VULKAN_MEMORY_TRACK,VULKAN_USE_LLM,VULKAN_OBJECT_TRACKING,VULKAN_CUSTOM_MEMORY_MANAGER_ENABLED"  -e linux,unix`
			if [ -z "$result" ]; then
				#echo $currentModule '==>' $userModule
				continue
			else
				echo $currentModule '==>' $userModule
				echo "=============== module " $currentModule 'referenced by' $userModule ============== >> result.txt
				echo $result >> result.txt
				echo "-----------------------------------" >> result.txt
				echo "" >> result.txt
			fi
		fi
	done
	
done
