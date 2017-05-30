/* zlibwrapper.c

        Copyright (C) 2017 Pawel Kolodziejski <aquadran at users.sourceforge.net>

        ---------------------------------------------------------------------------------

        Condition of use and distribution are the same than zlib :
 
  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgement in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.

  ---------------------------------------------------------------------------------
*/

#define _WIN32_WINNT 0x0501
#include <windows.h>
#include "zlib.h"

BOOL WINAPI DllMain(HINSTANCE hin, DWORD reason, LPVOID lpvReserved) { return TRUE; }

#define ZLIB_EXPORT __declspec(dllexport)

ZLIB_EXPORT int ZlibDecompress(unsigned char *src, unsigned int src_len, unsigned char *dst, unsigned int *dst_len)
{
	uLongf len = *dst_len;

	int status = uncompress((Bytef *)dst, &len, (Bytef *)src, (uLong)src_len);
	if (status == Z_OK)
		*dst_len = len;
	else
		*dst_len = 0;

	return status;
}

ZLIB_EXPORT int ZlibCompress(int compression_level, unsigned char *src, unsigned int src_len, unsigned char *dst, unsigned int *dst_len)
{
	uLongf len = *dst_len;

	int status = compress2((Bytef *)dst, &len, (Bytef *)src, (uLong)src_len, compression_level);
	if (status == Z_OK)
		*dst_len = len;
	else
		*dst_len = 0;

	return status;
}
