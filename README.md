# ModuleRef
ModuleRef finds module reference relationship base on header includes for C++ projects.

# Usage:
ModuleRef.exe -m  path [option]
	
	Required:
	-m --modules     : folders where Modules are defined.
	-r --ref-modules : folders where Modules are used.

	Options:
	-e --exclusive : exclude some specify directory, case insensitive.
	-s --skipMacro £ºskip codes surround by #if XXX #endif, case insensitive.
	-v --verbos    : show verbos result.
	-d --debug     : debug this tool to see if it counts like what you'v expected.