﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Runtime.InteropServices;

namespace ILRuntimeDebugEngine.AD7
{
    class AD7PendingBreakPoint : IDebugPendingBreakpoint2
    {
        private readonly AD7Engine _engine;
        private readonly IDebugBreakpointRequest2 _pBPRequest;
        private BP_REQUEST_INFO _bpRequestInfo;
        private AD7BoundBreakpoint _boundBreakpoint;

        public int StartLine { get; private set; }
        public int StartColumn { get; private set; }
        public int EndLine { get; private set; }
        public int EndColumn { get; private set; }
        public string DocumentName { get; set; }

        public AD7PendingBreakPoint(AD7Engine engine, IDebugBreakpointRequest2 pBPRequest)
        {
            var requestInfo = new BP_REQUEST_INFO[1];
            pBPRequest.GetRequestInfo(enum_BPREQI_FIELDS.BPREQI_BPLOCATION, requestInfo);
            _bpRequestInfo = requestInfo[0];
            _pBPRequest = pBPRequest;
            _engine = engine;

            //Enabled = true;

            var docPosition =
                (IDebugDocumentPosition2)Marshal.GetObjectForIUnknown(_bpRequestInfo.bpLocation.unionmember2);

            string documentName;
            docPosition.GetFileName(out documentName);
            var startPosition = new TEXT_POSITION[1];
            var endPosition = new TEXT_POSITION[1];
            docPosition.GetRange(startPosition, endPosition);

            DocumentName = documentName;
            StartLine = (int)startPosition[0].dwLine;
            StartColumn = (int)startPosition[0].dwColumn;

            EndLine = (int)endPosition[0].dwLine;
            EndColumn = (int)endPosition[0].dwColumn;
        }
        public int Bind()
        {
            _boundBreakpoint = new AD7BoundBreakpoint(_engine, this);
            TryBind();
            return Constants.S_OK;
        }

        public int CanBind(out IEnumDebugErrorBreakpoints2 ppErrorEnum)
        {
            throw new NotImplementedException();
        }

        public int Delete()
        {
            return Constants.S_OK;
        }

        public int Enable(int fEnable)
        {
            return Constants.S_OK;
        }

        public int EnumBoundBreakpoints(out IEnumDebugBoundBreakpoints2 ppEnum)
        {
            ppEnum = new AD7BoundBreakpointsEnum(new[] { _boundBreakpoint });
            return Constants.S_OK;
        }

        public int EnumErrorBreakpoints(enum_BP_ERROR_TYPE bpErrorType, out IEnumDebugErrorBreakpoints2 ppEnum)
        {
            ppEnum = null;
            return Constants.E_NOTIMPL;
        }

        public int GetBreakpointRequest(out IDebugBreakpointRequest2 ppBPRequest)
        {
            ppBPRequest = _pBPRequest;
            return Constants.S_OK;
        }

        public int GetState(PENDING_BP_STATE_INFO[] pState)
        {
            pState[0].state = enum_PENDING_BP_STATE.PBPS_ENABLED;
            return Constants.S_OK;
        }

        public int SetCondition(BP_CONDITION bpCondition)
        {
            throw new NotImplementedException();
        }

        public int SetPassCount(BP_PASSCOUNT bpPassCount)
        {
            throw new NotImplementedException();
        }

        public int Virtualize(int fVirtualize)
        {
            return Constants.S_OK;
        }

        internal bool TryBind()
        {
            try
            {
                using (var stream = File.OpenRead(DocumentName))
                {
                    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(stream), path: DocumentName);
                    TextLine textLine = syntaxTree.GetText().Lines[StartLine];
                    Location location = syntaxTree.GetLocation(textLine.Span);
                    SyntaxTree sourceTree = location.SourceTree;
                    SyntaxNode node = location.SourceTree.GetRoot().FindNode(location.SourceSpan, true, true);

                    var method = GetParentMethod<MethodDeclarationSyntax>(node.Parent);
                    string methodName = method.Identifier.Text;

                    var cl = GetParentMethod<ClassDeclarationSyntax>(method);
                    string className = cl.Identifier.Text;

                    var ns = GetParentMethod<NamespaceDeclarationSyntax>(method);
                    string nsname = ns.Name.ToString();

                    string name = string.Format("{0}.{1}", nsname, className);
                    System.Diagnostics.Debug.WriteLine("Bound");
                    return true;
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            return false;
        }

        private T GetParentMethod<T>(SyntaxNode node) where T : SyntaxNode
        {
            if (node == null)
                return null;

            if (node is T)
                return node as T;
            return GetParentMethod<T>(node.Parent);
        }
    }
}
