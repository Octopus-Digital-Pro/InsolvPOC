import { useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { ACCEPTED_FILE_TYPES } from '../services/fileProcessor';

interface FileDropZoneProps {
  onFileAccepted: (file: File) => void;
  isProcessing: boolean;
}

export default function FileDropZone({ onFileAccepted, isProcessing }: FileDropZoneProps) {
  const onDrop = useCallback(
    (acceptedFiles: File[]) => {
      if (acceptedFiles.length > 0 && !isProcessing) {
        onFileAccepted(acceptedFiles[0]);
      }
    },
    [onFileAccepted, isProcessing]
  );

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: ACCEPTED_FILE_TYPES,
    multiple: false,
    disabled: isProcessing,
  });

  return (
    <div
      {...getRootProps()}
      className={`
        flex flex-col items-center justify-center
        rounded-2xl border-2 border-dashed p-12
        transition-all duration-200 cursor-pointer
        ${isDragActive
          ? 'border-blue-500 bg-blue-50 scale-[1.01]'
          : 'border-gray-300 bg-gray-50 hover:border-gray-400 hover:bg-gray-100'
        }
        ${isProcessing ? 'opacity-50 pointer-events-none' : ''}
      `}
    >
      <input {...getInputProps()} />

      <div className="mb-4 rounded-full bg-white p-4 shadow-sm">
        <svg
          className={`h-10 w-10 ${isDragActive ? 'text-blue-500' : 'text-gray-400'}`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={1.5}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M12 16.5V9.75m0 0 3 3m-3-3-3 3M6.75 19.5a4.5 4.5 0 0 1-1.41-8.775 5.25 5.25 0 0 1 10.233-2.33 3 3 0 0 1 3.758 3.848A3.752 3.752 0 0 1 18 19.5H6.75Z"
          />
        </svg>
      </div>

      {isDragActive ? (
        <p className="text-lg font-medium text-blue-600">Drop the file here</p>
      ) : (
        <>
          <p className="text-lg font-medium text-gray-700">
            Drag & drop a document here
          </p>
          <p className="mt-1 text-sm text-gray-500">
            or <span className="text-blue-600 underline">browse files</span>
          </p>
          <p className="mt-3 text-xs text-gray-400">
            Supports PDF, PNG, JPG, WEBP
          </p>
        </>
      )}
    </div>
  );
}
