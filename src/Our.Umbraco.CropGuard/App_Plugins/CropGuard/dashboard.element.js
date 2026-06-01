import { LitElement, html, css, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

const API_BASE = "/umbraco/management/api/v1/cropguard";
const SECURITY = [{ scheme: "bearer", type: "http" }];

class CropGuardDashboard extends UmbElementMixin(LitElement) {
  static properties = {
    _crops: { state: true },
    _loading: { state: true },
    _error: { state: true },
    _newWidth: { state: true },
    _newHeight: { state: true },
    _newAlias: { state: true },
    _saving: { state: true },
  };

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
    }

    h2 {
      margin-top: 0;
    }

    .source-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .source-badge.dynamic { background: #d1fae5; color: #065f46; }
    .source-badge.static  { background: #dbeafe; color: #1e40af; }
    .source-badge.custom  { background: #fef3c7; color: #92400e; }

    .add-form {
      display: flex;
      gap: var(--uui-size-4);
      align-items: flex-end;
      flex-wrap: wrap;
      margin-bottom: var(--uui-size-layout-1);
    }

    .add-form label {
      display: flex;
      flex-direction: column;
      gap: 4px;
      font-size: 0.875rem;
    }

    table {
      width: 100%;
      border-collapse: collapse;
    }
    th, td {
      text-align: left;
      padding: 8px 12px;
      border-bottom: 1px solid var(--uui-color-border);
    }
    th {
      font-weight: 600;
      background: var(--uui-color-surface-alt);
    }

    .empty {
      color: var(--uui-color-disabled-contrast);
      padding: 24px 0;
      text-align: center;
    }
  `;

  constructor() {
    super();
    this._crops = [];
    this._loading = true;
    this._error = null;
    this._newWidth = "";
    this._newHeight = "";
    this._newAlias = "";
    this._saving = false;
  }

  connectedCallback() {
    super.connectedCallback();
    this._load();
  }

  async _load() {
    this._loading = true;
    this._error = null;
    const { data, error } = await tryExecute(
      this,
      umbHttpClient.get({ url: `${API_BASE}/crops`, security: SECURITY })
    );
    if (error) {
      this._error = error.message ?? String(error);
    } else {
      this._crops = data ?? [];
    }
    this._loading = false;
  }

  async _addCrop() {
    const width = parseInt(this._newWidth, 10);
    const height = parseInt(this._newHeight, 10);
    if (!width || !height || width <= 0 || height <= 0) {
      alert("Please enter valid positive Width and Height values.");
      return;
    }
    this._saving = true;
    const { error } = await tryExecute(
      this,
      umbHttpClient.post({
        url: `${API_BASE}/crops`,
        body: { width, height, alias: this._newAlias || null },
        security: SECURITY,
      })
    );
    if (error) {
      alert(`Failed to add crop: ${error.message ?? error}`);
    } else {
      this._newWidth = "";
      this._newHeight = "";
      this._newAlias = "";
      await this._load();
    }
    this._saving = false;
  }

  async _removeCrop(width, height) {
    if (!confirm(`Remove crop ${width}×${height}?`)) return;
    const { error } = await tryExecute(
      this,
      umbHttpClient.delete({ url: `${API_BASE}/crops/${width}/${height}`, security: SECURITY })
    );
    if (error) {
      alert(`Failed to remove crop: ${error.message ?? error}`);
      return;
    }
    await this._load();
  }

  async _refresh() {
    await tryExecute(
      this,
      umbHttpClient.post({ url: `${API_BASE}/crops/refresh`, security: SECURITY })
    );
    await this._load();
  }

  render() {
    return html`
      <uui-box headline="CropGuard">
        <p>
          Only the crops listed below will be allowed through the ImageSharp processing pipeline.
          Requests with dimensions not in this list will receive a <code>400 Bad Request</code>.
        </p>

        <h3>Add Custom Crop</h3>
        <div class="add-form">
          <label>
            Width (px)
            <uui-input
              type="number"
              min="1"
              placeholder="e.g. 800"
              .value=${this._newWidth}
              @input=${(e) => (this._newWidth = e.target.value)}
            ></uui-input>
          </label>
          <label>
            Height (px)
            <uui-input
              type="number"
              min="1"
              placeholder="e.g. 600"
              .value=${this._newHeight}
              @input=${(e) => (this._newHeight = e.target.value)}
            ></uui-input>
          </label>
          <label>
            Alias (optional)
            <uui-input
              type="text"
              placeholder="e.g. heroImage"
              .value=${this._newAlias}
              @input=${(e) => (this._newAlias = e.target.value)}
            ></uui-input>
          </label>
          <uui-button
            look="primary"
            label="Add Crop"
            ?disabled=${this._saving}
            @click=${this._addCrop}
          >Add Crop</uui-button>
        </div>

        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:8px;">
          <h3 style="margin:0">Allowed Crops</h3>
          <uui-button look="outline" label="Refresh" @click=${this._refresh}>
            Refresh cache
          </uui-button>
        </div>

        ${this._loading
          ? html`<uui-loader></uui-loader>`
          : this._error
          ? html`<uui-tag color="danger">Error: ${this._error}</uui-tag>`
          : this._crops.length === 0
          ? html`<p class="empty">No crops configured yet. Add a custom crop above or define Image Cropper data types in Umbraco.</p>`
          : html`
            <table>
              <thead>
                <tr>
                  <th>Width</th>
                  <th>Height</th>
                  <th>Alias</th>
                  <th>Source</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                ${this._crops.map(
                  (crop) => html`
                    <tr>
                      <td>${crop.width}px</td>
                      <td>${crop.height}px</td>
                      <td>${crop.alias ?? "—"}</td>
                      <td>
                        <span class="source-badge ${crop.source.toLowerCase()}">${crop.source}</span>
                      </td>
                      <td>
                        ${crop.source === "Custom"
                          ? html`
                              <uui-button
                                look="outline"
                                color="danger"
                                label="Remove"
                                @click=${() => this._removeCrop(crop.width, crop.height)}
                              >Remove</uui-button>
                            `
                          : nothing}
                      </td>
                    </tr>
                  `
                )}
              </tbody>
            </table>
          `}
      </uui-box>
    `;
  }
}

customElements.define("crop-guard-dashboard", CropGuardDashboard);

export default CropGuardDashboard;
